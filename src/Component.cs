using System.Diagnostics;
using System.Text.RegularExpressions;

public class Component
{
  public string PatternString { get; init; }
  public Regex RegularExpression { get; init; }
  public IList<string> GroupNameList { get; init; }

  private Component(string patternString, Regex regularExpression, IList<string> groupNameList)
  {
    this.PatternString = patternString;
    this.RegularExpression = regularExpression;
    this.GroupNameList = groupNameList;
  }

  // Ref: https://wicg.github.io/urlpattern/#compile-a-component
  static public Component CompileAComponent(string? input, EncodingCallback encodingCallback, Options options)
  {
    if (input is null) input = "*";
    var partList = PatternParser.ParseAPatternString(input, options, encodingCallback);
    var (regularExpressionString, nameList) = GenerateARegularExpressionAndNameList(partList, options);
    RegexOptions flags = 0;
    if (options.ignoreCase) flags = RegexOptions.IgnoreCase;
    var regularExpression = new Regex(regularExpressionString, flags);
    var patternString = GenerateAPatternString(partList, options);
    return new Component(patternString, regularExpression, nameList);
  }

  // Ref: https://wicg.github.io/urlpattern/#generate-a-regular-expression-and-name-list
  private static (string, IList<string>) GenerateARegularExpressionAndNameList(IList<Part> partList, Options options)
  {
    var result = "^";
    var nameList = new List<string>();

    foreach (var part in partList)
    {
      if (part.Type is PartType.FixedText)
      {
        if (part.Modifier is PartModifier.None)
        {
          result += PatternParser.EscapeARegexpString(part.Value);
        }
        else
        {
          result += "(?:";
          result += PatternParser.EscapeARegexpString(part.Value);
          result += ")";
          result += Component.ConvertAModifierToAString(part.Modifier);
        }
        continue;
      }

      Debug.Assert(part.Name != string.Empty);
      nameList.Add(part.Name);

      var regexpValue = part.Value;
      if (part.Type is PartType.SegmentWildcard)
      {
        regexpValue = PatternParser.GenerateASegmentWildcardRegexp(options);
      }
      else if (part.Type is PartType.FullWildcard)
      {
        regexpValue = PatternParser.FullWildcardRegexpValue;
      }

      if (part.Prefix == string.Empty && part.Suffix == string.Empty)
      {
        if (part.Modifier is PartModifier.None or PartModifier.Optional)
        {
          result += "(";
          result += regexpValue;
          result += ")";
          result += Component.ConvertAModifierToAString(part.Modifier);
        }
        else
        {
          result += "((?:";
          result += regexpValue;
          result += ")";
          result += Component.ConvertAModifierToAString(part.Modifier);
          result += ")";
        }
        continue;
      }

      if (part.Modifier is PartModifier.None or PartModifier.Optional)
      {
        result += "(?:";
        result += PatternParser.EscapeARegexpString(part.Prefix);
        result += "(";
        result += regexpValue;
        result += ")";
        result += PatternParser.EscapeARegexpString(part.Suffix);
        result += ")";
        result += Component.ConvertAModifierToAString(part.Modifier);
        continue;
      }
      Debug.Assert(part.Modifier is PartModifier.ZeroOrMore or PartModifier.OneOrMore);
      Debug.Assert(part.Prefix != string.Empty || part.Suffix != string.Empty);
      result += "(?:";
      result += PatternParser.EscapeARegexpString(part.Prefix);
      result += "((?:";
      result += regexpValue;
      result += ")(?:";
      result += PatternParser.EscapeARegexpString(part.Suffix);
      result += PatternParser.EscapeARegexpString(part.Prefix);
      result += "(?:";
      result += regexpValue;
      result += "))*)";
      result += PatternParser.EscapeARegexpString(part.Suffix);
      result += ")";
      if (part.Modifier is PartModifier.ZeroOrMore) result += "?";
    }
    result += "$";
    return (result, nameList);
  }

  // Ref: https://wicg.github.io/urlpattern/#convert-a-modifier-to-a-string
  static private string ConvertAModifierToAString(PartModifier modifier)
  {
    if (modifier is PartModifier.ZeroOrMore) return "*";
    if (modifier is PartModifier.ZeroOrMore) return "?";
    if (modifier is PartModifier.ZeroOrMore) return "+";
    return string.Empty;
  }

  // Ref: https://wicg.github.io/urlpattern/#generate-a-pattern-string
  static private string GenerateAPatternString(IList<Part> partList, Options options)
  {
    var result = string.Empty;
    var indexList = Utils.GetTheIndices(partList);
    foreach (var index in indexList)
    {
      var part = partList[index];
      var previousPart = (index > 0) ? partList[index - 1] : null;
      var nextPart = (index < indexList.Count - 1) ? partList[index + 1] : null;

      if (part.Type is PartType.FixedText)
      {
        if (part.Modifier is PartModifier.None)
        {
          result += PatternParser.EscapeARegexpString(part.Value);
          continue;
        }
        result += "{";
        result += PatternParser.EscapeARegexpString(part.Value);
        result += "}";
        result += Component.ConvertAModifierToAString(part.Modifier);
        continue;
      }

      var customName = (Utils.IsAsciiDigit(part.Name[0]) == false);
      var needsGrouping = (part.Suffix != string.Empty || (part.Prefix != string.Empty && part.Prefix != options.PrefixCodePoint));

      if (
        needsGrouping is false &&
        customName is true &&
        part.Type is PartType.SegmentWildcard &&
        part.Modifier is PartModifier.None &&
        nextPart is not null &&
        nextPart.Prefix == string.Empty &&
        nextPart.Suffix == string.Empty
      )
      {
        if (nextPart.Type is PartType.FixedText)
        {
          needsGrouping = Utils.IsAValidNameCodePoint(nextPart.Value.First(), false);
        }
        else
        {
          needsGrouping = Utils.IsAsciiDigit(nextPart.Name[0]);
        }
      }

      if (
        needsGrouping is false &&
        part.Prefix == string.Empty &&
        previousPart is not null &&
        previousPart.Type is PartType.FixedText &&
        previousPart.Value.Last().ToString() == options.PrefixCodePoint
      )
      {
        needsGrouping = true;
      }

      Debug.Assert(part.Name != string.Empty && part.Name is not null);

      if (needsGrouping) result += "{";
      result += PatternParser.EscapeARegexpString(part.Prefix);

      if (customName)
      {
        result += ":";
        result += part.Name;
      }

      if (part.Type is PartType.Regexp)
      {
        result += "(";
        result += part.Value;
        result += ")";
      }
      else if (part.Type is PartType.SegmentWildcard)
      {
        result += "(";
        result += PatternParser.GenerateASegmentWildcardRegexp(options);
        result += ")";
      }
      else if (part.Type is PartType.FullWildcard)
      {
        if (customName is false && (
          previousPart is null ||
          previousPart.Type is PartType.FixedText ||
          previousPart.Modifier is PartModifier.None ||
          needsGrouping is true ||
          part.Prefix != string.Empty))
        {
          result += "*";
        }
        else
        {
          result += "(";
          result += PatternParser.FullWildcardRegexpValue;
          result += ")";
        }
      }

      if (part.Type is PartType.SegmentWildcard &&
      customName is true &&
      part.Suffix != string.Empty &&
      Utils.IsAValidNameCodePoint(part.Suffix.First(), false))
      {
        result += "\\";
      }

      result += Component.EscapeAPatternString(part.Suffix);
      if (needsGrouping is true) result += "}";
      result += ConvertAModifierToAString(part.Modifier);
    }

    return result;
  }

  // Ref: https://wicg.github.io/urlpattern/#escape-a-pattern-string
  public static string EscapeAPatternString(string input)
  {
    Debug.Assert(Utils.IsAsciiString(input));

    var result = string.Empty;
    var index = 0;

    while (index < input.Length)
    {
      var c = input[index];
      index += 1;

      if (c is '+'
        or '*'
        or '?'
        or ':'
        or '{'
        or '}'
        or '('
        or ')'
        or '\\')
      {
        result += "\\";
      }

      result += c;
    }

    return result;
  }
}