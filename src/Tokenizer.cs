using System.Diagnostics;

public enum TokenType
{
  Open,
  Close,
  Regexp,
  Name,
  Char,
  EscapedChar,
  OtherModifier,
  Asterisk,
  End,
  InvalidChar,
}

public record Token(TokenType Type = TokenType.InvalidChar, int Index = 0, string Value = "");

public enum TokenizePolicy
{
  Strict,
  Lenient
}

public class Tokenizer
{
  public readonly IList<Token> TokenList = new List<Token>();
  public string Input = string.Empty;
  public TokenizePolicy Policy { get; init; } = TokenizePolicy.Strict;
  public int Index = 0;
  public int NextIndex = 0;
  public char CodePoint;

  private Tokenizer()
  {
  }

  // Ref: https://wicg.github.io/urlpattern/#get-the-next-code-point
  private void GetTheNextCodePoint()
  {
    this.CodePoint = this.Input[this.NextIndex];
    this.NextIndex += 1;
  }

  // Ref: https://wicg.github.io/urlpattern/#seek-and-get-the-next-code-point
  private void SeekAndGetTheNextCodePoint(int index)
  {
    this.NextIndex = index;
    this.GetTheNextCodePoint();
  }

  // Ref: https://wicg.github.io/urlpattern/#add-a-token
  private void AddToken(TokenType type, int nextPosition, int valuePosition, int valueLength)
  {
    var token = new Token(type, this.Index, this.Input.Substring(valuePosition, valueLength));
    this.TokenList.Add(token);
    this.Index = nextPosition;
  }

  // Ref: https://wicg.github.io/urlpattern/#add-a-token-with-default-length
  private void AddTokenWithDefaultLength(TokenType type, int nextPosition, int valuePosition)
  {
    var computedLength = nextPosition - valuePosition;
    this.AddToken(type, nextPosition, valuePosition, computedLength);
  }

  // Ref: https://wicg.github.io/urlpattern/#add-a-token-with-default-position-and-length
  private void AddTokenWithDefaultPositionAndLength(TokenType type)
  {
    this.AddTokenWithDefaultLength(type, this.NextIndex, this.Index);
  }

  // Ref: https://wicg.github.io/urlpattern/#process-a-tokenizing-error
  private void ProcessTokenizingError(int nextPosition, int valuePosition)
  {
    if (this.Policy == TokenizePolicy.Strict)
    {
      throw new Exception($"TokenizingError at {valuePosition}:{nextPosition}");
    }

    Debug.Assert(this.Policy == TokenizePolicy.Lenient, "Can only ignore errors if set to Lenient");

    this.AddTokenWithDefaultLength(TokenType.InvalidChar, nextPosition, valuePosition);
  }


  public static IList<Token> Tokenize(string input, TokenizePolicy policy)
  {
    var tokenizer = new Tokenizer
    {
      Input = input,
      Policy = policy
    };

    while (tokenizer.Index < input.Length)
    {
      tokenizer.SeekAndGetTheNextCodePoint(tokenizer.Index);

      if (tokenizer.CodePoint is '*')
      {
        tokenizer.AddTokenWithDefaultPositionAndLength(TokenType.Asterisk);
        continue;
      }

      if (tokenizer.CodePoint is '+' or '?')
      {
        tokenizer.AddTokenWithDefaultPositionAndLength(TokenType.OtherModifier);
        continue;
      }

      if (tokenizer.CodePoint is '\\')
      {
        if (tokenizer.Index == tokenizer.Input.Length - 1)
        {
          tokenizer.ProcessTokenizingError(tokenizer.NextIndex, tokenizer.Index);
          continue;
        }

        var escapedIndex = tokenizer.NextIndex;
        tokenizer.GetTheNextCodePoint();
        tokenizer.AddTokenWithDefaultLength(TokenType.EscapedChar, tokenizer.NextIndex, escapedIndex);
        continue;
      }

      if (tokenizer.CodePoint is '{')
      {
        tokenizer.AddTokenWithDefaultPositionAndLength(TokenType.Open);
        continue;
      }

      if (tokenizer.CodePoint is '}')
      {
        tokenizer.AddTokenWithDefaultPositionAndLength(TokenType.Close);
      }

      if (tokenizer.CodePoint is ':')
      {
        var namePosition = tokenizer.NextIndex;
        var nameStart = namePosition;

        while (namePosition < tokenizer.Input.Length)
        {
          tokenizer.SeekAndGetTheNextCodePoint(namePosition);
          var firstCodePoint = namePosition == nameStart;
          var validCodePoint = Utils.IsAValidNameCodePoint(tokenizer.CodePoint, firstCodePoint);
          if (validCodePoint == false)
          {
            break;
          }

          namePosition = tokenizer.NextIndex;
        }

        if (namePosition <= nameStart)
        {
          tokenizer.ProcessTokenizingError(nameStart, tokenizer.Index);
          continue;
        }

        tokenizer.AddTokenWithDefaultLength(TokenType.Name, namePosition, nameStart);
        continue;
      }

      if (tokenizer.CodePoint is '(')
      {
        var depth = 1;
        var regexpPosition = tokenizer.NextIndex;
        var regexpStart = regexpPosition;
        var error = false;

        while (regexpPosition < tokenizer.Input.Length)
        {
          tokenizer.SeekAndGetTheNextCodePoint(regexpPosition);
          if (Utils.IsAscii(tokenizer.CodePoint) == false)
          {
            tokenizer.ProcessTokenizingError(regexpStart, tokenizer.Index);
            error = true;
            break;
          }

          if (regexpPosition == regexpStart && tokenizer.CodePoint == '?')
          {
            tokenizer.ProcessTokenizingError(regexpStart, tokenizer.Index);
            error = true;
            break;
          }

          if (tokenizer.CodePoint == '\\')
          {
            if (regexpPosition == tokenizer.Input.Length - 1)
            {
              tokenizer.ProcessTokenizingError(regexpStart, tokenizer.Index);
              error = true;
              break;
            }

            tokenizer.GetTheNextCodePoint();

            if (Utils.IsAscii(tokenizer.CodePoint) == false)
            {
              tokenizer.ProcessTokenizingError(regexpStart, tokenizer.Index);
              error = true;
              break;
            }

            regexpPosition = tokenizer.NextIndex;
            continue;
          }

          if (tokenizer.CodePoint is ')')
          {
            depth -= 1;
            if (depth is 0)
            {
              regexpPosition = tokenizer.NextIndex;
              break;
            }
          }
          else if (tokenizer.CodePoint is '(')
          {
            depth += 1;
            if (regexpPosition == tokenizer.Input.Length - 1)
            {
              tokenizer.ProcessTokenizingError(regexpStart, tokenizer.Index);
              error = true;
              break;
            }

            var temporaryPosition = tokenizer.NextIndex;
            tokenizer.GetTheNextCodePoint();
            if (tokenizer.CodePoint is not '?')
            {
              tokenizer.ProcessTokenizingError(regexpStart, tokenizer.Index);
              error = true;
              break;
            }

            tokenizer.NextIndex = temporaryPosition;
          }

          regexpPosition = tokenizer.NextIndex;
        }

        if (error)
        {
          continue;
        }

        if (depth is not 0)
        {
          tokenizer.ProcessTokenizingError(regexpStart, tokenizer.Index);
          continue;
        }

        var regexpLength = regexpPosition - regexpStart - 1;
        if (regexpLength is 0)
        {
          tokenizer.ProcessTokenizingError(regexpStart, tokenizer.Index);
          continue;
        }

        tokenizer.AddToken(TokenType.Regexp, regexpPosition, regexpStart, regexpLength);
        continue;
      }

      tokenizer.AddTokenWithDefaultPositionAndLength(TokenType.Char);
    }

    tokenizer.AddTokenWithDefaultLength(TokenType.End, tokenizer.Index, tokenizer.Index);
    return tokenizer.TokenList;
  }


}
