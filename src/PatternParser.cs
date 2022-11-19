using System.Diagnostics;

public delegate string EncodingCallback(string input);

public enum PartType
{
  FixedText,
  Regexp,
  SegmentWildcard,
  FullWildcard
}

public enum PartModifier
{
  None,
  Optional,
  ZeroOrMore,
  OneOrMore
}

// Ref: https://wicg.github.io/urlpattern/#part
public record Part(PartType Type, string Value, PartModifier Modifier)
{
  public string Name = string.Empty;
  public string Prefix = string.Empty;
  public string Suffix = string.Empty;

}

public record Options(string DelimiterCodePoint, string PrefixCodePoint, bool ignoreCase = false)
{
  static public Options Default = new Options("", "");
  static public Options Hostname = new Options(".", "");
  static public Options Pathname = new Options("/", "/");

  public Options(Options other)
  {
    this.DelimiterCodePoint = other.DelimiterCodePoint;
    this.PrefixCodePoint = other.PrefixCodePoint;
    this.ignoreCase = other.ignoreCase;
  }
}

public class PatternParser
{
  // Ref: https://wicg.github.io/urlpattern/#full-wildcard-regexp-value
  public static string FullWildcardRegexpValue = ".*";

  public IList<Token> TokenList = new List<Token>();
  public EncodingCallback EncodingCallback { get; init; }
  public string SegmentWildcardRegexp { get; init; }
  public IList<Part> PartList = new List<Part>();
  public string PendingFixedValue = string.Empty;
  public int Index = 0;
  public int NextNumericName = 0;

  private PatternParser(EncodingCallback encodingCallback, string segmentWildcardRegexp)
  {
    this.EncodingCallback = encodingCallback;
    this.SegmentWildcardRegexp = segmentWildcardRegexp;
  }

  // Ref: https://wicg.github.io/urlpattern/#parse-a-pattern-string
  public static IList<Part> ParseAPatternString(string input, Options options, EncodingCallback encodingCallback)
  {
    var parser = new PatternParser(encodingCallback, PatternParser.GenerateASegmentWildcardRegexp(options));
    parser.TokenList = Tokenizer.Tokenize(input, TokenizePolicy.Strict);

    while (parser.Index < parser.TokenList.Count)
    {
      var charToken = parser.TryToConsumeAToken(TokenType.Char);
      var nameToken = parser.TryToConsumeAToken(TokenType.Name);
      var regexpOrWildcardToken = parser.TryToConsumeARegexpOrWildcardToken(nameToken);

      if (nameToken is not null || regexpOrWildcardToken is not null)
      {
        // If there is a matching group, we need to add the part immediately.
        var prefix = string.Empty;
        if (charToken is not null) prefix = charToken.Value;
        if (prefix != string.Empty && prefix != options.PrefixCodePoint)
        {
          parser.PendingFixedValue += prefix;
          prefix = string.Empty;
        }

        parser.MaybeAddAPartFromThePendingFixedValue();
        var modifiedToken = parser.TryToConsumeAModifierToken();
        parser.AddAPart(prefix, nameToken, regexpOrWildcardToken, string.Empty, modifiedToken);
        continue;
      }

      var fixedToken = charToken;
      // If there was no matching group, then we need to buffer any fixed text.
      // We want to collect as much text as possible before adding it as a 
      // "fixed-text" part.
      if (fixedToken is null) fixedToken = parser.TryToConsumeAToken(TokenType.EscapedChar);
      if (fixedToken is not null)
      {
        parser.PendingFixedValue += fixedToken.Value;
        continue;
      }

      var openToken = parser.TryToConsumeAToken(TokenType.Open);
      if (openToken is not null)
      {
        var prefix = parser.ConsumeText();
        nameToken = parser.TryToConsumeAToken(TokenType.Name);
        regexpOrWildcardToken = parser.TryToConsumeARegexpOrWildcardToken(nameToken);
        var suffix = parser.ConsumeText();
        parser.ConsumeARequiredToken(TokenType.Close);
        var modifiedToken = parser.TryToConsumeAModifierToken();
        parser.AddAPart(prefix, nameToken, regexpOrWildcardToken, suffix, modifiedToken);
        continue;
      }

      parser.MaybeAddAPartFromThePendingFixedValue();
      parser.ConsumeARequiredToken(TokenType.End);
    }

    return parser.PartList;
  }

  // Ref: https://wicg.github.io/urlpattern/#generate-a-segment-wildcard-regexp
  public static string GenerateASegmentWildcardRegexp(Options options)
  {
    var result = "[^";
    result += PatternParser.EscapeARegexpString(options.DelimiterCodePoint);
    result += "]+?";
    return result;
  }

  // Ref: https://wicg.github.io/urlpattern/#try-to-consume-a-token
  private Token? TryToConsumeAToken(TokenType type)
  {
    Debug.Assert(this.Index < this.TokenList.Count);

    var nextToken = this.TokenList[this.Index];

    if (nextToken.Type != type)
    {
      return null;
    }

    this.Index += 1;
    return nextToken;
  }

  // Ref: https://wicg.github.io/urlpattern/#try-to-consume-a-modifier-token
  private Token? TryToConsumeAModifierToken()
  {
    var token = this.TryToConsumeAToken(TokenType.OtherModifier);

    if (token is not null)
    {
      return token;
    }

    token = this.TryToConsumeAToken(TokenType.Asterisk);
    return token;
  }

  // Ref: https://wicg.github.io/urlpattern/#try-to-consume-a-regexp-or-wildcard-token
  private Token? TryToConsumeARegexpOrWildcardToken(Token? nameToken)
  {
    var token = this.TryToConsumeAToken(TokenType.Regexp);

    if (nameToken is null && token is null)
    {
      token = this.TryToConsumeAToken(TokenType.Asterisk);
    }

    return token;
  }

  // Ref: https://wicg.github.io/urlpattern/#consume-a-required-token
  private Token ConsumeARequiredToken(TokenType type)
  {
    var result = this.TryToConsumeAToken(type);

    if (result is null)
    {
      throw new Exception("TypeError");
    }

    return result;
  }

  // Ref: https://wicg.github.io/urlpattern/#consume-text
  private string ConsumeText()
  {
    var result = string.Empty;

    while (true)
    {
      var token = this.TryToConsumeAToken(TokenType.Char);

      if (token is null)
      {
        token = this.TryToConsumeAToken(TokenType.EscapedChar);
      }

      if (token is null)
      {
        break;
      }

      result += token.Value;
    }

    return result;
  }

  // Ref: https://wicg.github.io/urlpattern/#maybe-add-a-part-from-the-pending-fixed-value
  private void MaybeAddAPartFromThePendingFixedValue()
  {
    if (this.PendingFixedValue == string.Empty) return;

    var encodedValue = this.EncodingCallback(this.PendingFixedValue);
    this.PendingFixedValue = string.Empty;
    var part = new Part(PartType.FixedText, encodedValue, PartModifier.None);
    this.PartList.Add(part);
  }

  // Ref: https://wicg.github.io/urlpattern/#add-a-part
  private void AddAPart(string prefix, Token? nameToken, Token? regexpOrWildcardToken, string suffix, Token? modifierToken)
  {
    var modifier = PartModifier.None;
    if (modifierToken is not null)
    {
      if (modifierToken.Value == "?") modifier = PartModifier.Optional;
      else if (modifierToken.Value == "*") modifier = PartModifier.ZeroOrMore;
      else if (modifierToken.Value == "+") modifier = PartModifier.OneOrMore;
    }

    if (nameToken is null && regexpOrWildcardToken is null && modifier is PartModifier.None)
    {
      // This was a "{foo}" grouping. We add this to the pending fixed value so
      // that it will be combined with any previous or subsequent text.
      this.PendingFixedValue += prefix;
      return;
    }

    this.MaybeAddAPartFromThePendingFixedValue();

    if (nameToken is null && regexpOrWildcardToken is null)
    {
      // This was a "{foo}?" grouping. The modifier means we cannot combine it
      // with other text. Therefore we add it as a part immediately.
      Debug.Assert(suffix == string.Empty);
      if (prefix == string.Empty) return;
      var encodedValue = this.EncodingCallback(prefix);
      var part = new Part(PartType.FixedText, encodedValue, modifier);
      this.PartList.Add(part);
      return;
    }

    var regexpValue = string.Empty;

    // Next, we convert the regexp or wildcard token into a regular expression.
    if (regexpOrWildcardToken is null)
    {
      regexpValue = this.SegmentWildcardRegexp;
    }
    else if (regexpOrWildcardToken.Type is TokenType.Asterisk)
    {
      regexpValue = PatternParser.FullWildcardRegexpValue;
    }
    else
    {
      regexpValue = regexpOrWildcardToken.Value;
    }

    var type = PartType.Regexp;
    // Next, we convert regexp value into a part type. We make sure to go to a
    // regular expression first so that an equivalent "regexp" token will be
    // treated the same as a "name" or "asterisk" token.
    if (regexpValue == this.SegmentWildcardRegexp)
    {
      type = PartType.SegmentWildcard;
      regexpValue = string.Empty;
    }
    else if (regexpValue == PatternParser.FullWildcardRegexpValue)
    {
      type = PartType.FullWildcard;
      regexpValue = string.Empty;
    }

    var name = string.Empty;
    // Next, we determine the part name. This can be explicitly provided by a
    // "name" token or be automatically assigned.
    if (nameToken is not null)
    {
      name = nameToken.Value;
    }
    else if (regexpOrWildcardToken is not null)
    {
      name = this.NextNumericName.ToString();
      this.NextNumericName += 1;
    }

    if (this.IsADuplicateName(name))
    {
      throw new Exception("TypeError");
    }

    {
      var encodedPrefix = this.EncodingCallback(prefix);
      // Finally, we encode the fixed text values and create the part.
      var encodedSuffix = this.EncodingCallback(suffix);
      var part = new Part(type, regexpValue, modifier)
      {
        Name = name,
        Prefix = encodedPrefix,
        Suffix = encodedSuffix
      };
      this.PartList.Add(part);
    }
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-duplicate-name
  private bool IsADuplicateName(string name)
  {
    foreach (var part in this.PartList)
    {
      if (part.Name == name) return true;
    }
    return false;
  }

  // Ref: https://wicg.github.io/urlpattern/#escape-a-regexp-string
  public static string EscapeARegexpString(string input)
  {
    Debug.Assert(Utils.IsAsciiString(input));

    var result = string.Empty;
    var index = 0;

    while (index < input.Length)
    {
      var c = input[index];
      index += 1;

      if (c
      is '.'
      or '+'
      or '*'
      or '?'
      or '^'
      or '$'
      or '{'
      or '}'
      or '('
      or ')'
      or '['
      or ']'
      or '|'
      or '/'
      or '\\')
      {
        result += "\\";
      }

      result += c;
    }

    return result;
  }
}