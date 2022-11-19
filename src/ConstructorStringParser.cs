using System.Diagnostics;

namespace URLPatternWebApi;

public enum ConstructorStringParserState
{
  Init,
  Protocol,
  Authority,
  Username,
  Password,
  Hostname,
  Port,
  Pathname,
  Search,
  Hash,
  Done
}

public class ConstructorStringParser
{
  public string Input { get; init; }
  public IList<Token> TokenList { get; init; }
  public URLPatternInit Result = new URLPatternInit();
  public int ComponentStart = 0;
  public int TokenIndex = 0;
  public int TokenIncrement = 1;
  public int GroupDepth = 0;
  public int HostnameIPv6BracketDepth = 0;
  public bool ProtocolMatchesSpecialSchemeFlag = false;
  public ConstructorStringParserState State = ConstructorStringParserState.Init;

  private ConstructorStringParser(string input)
  {
    this.Input = input;
    this.TokenList = Tokenizer.Tokenize(input, TokenizePolicy.Lenient);
  }

  // Ref: https://wicg.github.io/urlpattern/#parse-a-constructor-string
  public static URLPatternInit ParseAConstructorString(string input)
  {
    var parser = new ConstructorStringParser(input);

    while (parser.TokenIndex < parser.TokenList.Count)
    {
      parser.TokenIncrement = 1;

      if (parser.TokenList[parser.TokenIndex].Type is TokenType.End)
      {
        if (parser.State is ConstructorStringParserState.Init)
        {
          parser.Rewind();

          if (parser.IsAHashPrefix())
          {
            parser.ChangeState(ConstructorStringParserState.Search, 1);
            parser.Result.Hash = string.Empty;
          }
          else
          {
            parser.ChangeState(ConstructorStringParserState.Pathname, 0);
            parser.Result.Search = string.Empty;
            parser.Result.Hash = string.Empty;
          }
          parser.TokenIndex += parser.TokenIncrement;
          continue;
        }

        if (parser.State is ConstructorStringParserState.Authority)
        {
          parser.RewindAndSetState(ConstructorStringParserState.Hostname);
          parser.TokenIndex += parser.TokenIncrement;
          continue;
        }

        parser.ChangeState(ConstructorStringParserState.Done, 0);
        break;
      }

      if (parser.IsAGroupOpen())
      {
        parser.GroupDepth += 1;
        parser.TokenIndex += parser.TokenIncrement;
        continue;
      }

      if (parser.GroupDepth > 0)
      {
        if (parser.IsAGroupClose())
        {
          parser.GroupDepth -= 1;
        }
        else
        {
          parser.TokenIndex += parser.TokenIncrement;
          continue;
        }
      }

      switch (parser.State)
      {
        case ConstructorStringParserState.Init:
          if (parser.IsAProtocolSuffix())
          {
            parser.Result.Username = string.Empty;
            parser.Result.Password = string.Empty;
            parser.Result.Hostname = string.Empty;
            parser.Result.Port = string.Empty;
            parser.Result.Pathname = string.Empty;
            parser.Result.Search = string.Empty;
            parser.Result.Hash = string.Empty;
            parser.RewindAndSetState(ConstructorStringParserState.Protocol);
          }
          break;

        case ConstructorStringParserState.Protocol:
          if (parser.IsAProtocolSuffix())
          {
            parser.ComputeProtocolMatchesASpecialSchemeFlag();
            if (parser.ProtocolMatchesSpecialSchemeFlag)
            {
              parser.Result.Pathname = "/";
            }
            var nextState = ConstructorStringParserState.Pathname;
            var skip = 1;
            if (parser.NextIsAuthoritySlashes())
            {
              nextState = ConstructorStringParserState.Authority;
              skip = 3;
            }
            else if (parser.ProtocolMatchesSpecialSchemeFlag)
            {
              nextState = ConstructorStringParserState.Authority;
            }

            parser.ChangeState(nextState, skip);
          }
          break;

        case ConstructorStringParserState.Authority:
          if (parser.IsAnIdentityTerminator())
          {
            parser.RewindAndSetState(ConstructorStringParserState.Username);
          }
          else if (parser.IsAPathnameStart() || parser.IsASearchPrefix() || parser.IsAHashPrefix())
          {
            parser.RewindAndSetState(ConstructorStringParserState.Hostname);
          }
          break;

        case ConstructorStringParserState.Username:
          if (parser.IsAPasswordPrefix())
          {
            parser.ChangeState(ConstructorStringParserState.Password, 1);
          }
          else if (parser.IsAnIdentityTerminator())
          {
            parser.ChangeState(ConstructorStringParserState.Hostname, 1);
          }
          break;

        case ConstructorStringParserState.Password:
          if (parser.IsAnIdentityTerminator())
          {
            parser.ChangeState(ConstructorStringParserState.Hostname, 1);
          }
          break;

        case ConstructorStringParserState.Hostname:
          if (parser.IsAnIPv6Open())
          {
            parser.HostnameIPv6BracketDepth += 1;
          }
          else if (parser.IsAnIPv6Close())
          {
            parser.HostnameIPv6BracketDepth -= 1;
          }
          else if (parser.IsAPortPrefix() && parser.HostnameIPv6BracketDepth == 0)
          {
            parser.ChangeState(ConstructorStringParserState.Port, 1);
          }
          else if (parser.IsAPathnameStart())
          {
            parser.ChangeState(ConstructorStringParserState.Pathname, 0);
          }
          else if (parser.IsASearchPrefix())
          {
            parser.ChangeState(ConstructorStringParserState.Search, 1);
          }
          else if (parser.IsAHashPrefix())
          {
            parser.ChangeState(ConstructorStringParserState.Hash, 1);
          }
          break;

        case ConstructorStringParserState.Port:
          if (parser.IsAPathnameStart())
          {
            parser.ChangeState(ConstructorStringParserState.Pathname, 0);
          }
          else if (parser.IsASearchPrefix())
          {
            parser.ChangeState(ConstructorStringParserState.Search, 1);
          }
          else if (parser.IsAHashPrefix())
          {
            parser.ChangeState(ConstructorStringParserState.Hash, 1);
          }
          break;

        case ConstructorStringParserState.Pathname:
          if (parser.IsASearchPrefix())
          {
            parser.ChangeState(ConstructorStringParserState.Search, 1);
          }
          else if (parser.IsAHashPrefix())
          {
            parser.ChangeState(ConstructorStringParserState.Hash, 1);
          }
          break;

        case ConstructorStringParserState.Search:
          if (parser.IsAHashPrefix())
          {
            parser.ChangeState(ConstructorStringParserState.Hash, 1);
          }
          break;

        case ConstructorStringParserState.Hash:
          break;

        case ConstructorStringParserState.Done:
        default:
          Debug.Assert(false, "This step is never reached");
          break;
      }
      parser.TokenIndex += parser.TokenIncrement;
    }

    return parser.Result;
  }

  // Ref: https://wicg.github.io/urlpattern/#change-state
  private void ChangeState(ConstructorStringParserState newState, int skip)
  {
    if (this.State is not ConstructorStringParserState.Init or ConstructorStringParserState.Authority or ConstructorStringParserState.Done)
    {
      switch (this.State)
      {
        case ConstructorStringParserState.Protocol:
          this.Result.Protocol = this.MakeAComponentString();
          break;
        case ConstructorStringParserState.Username:
          this.Result.Username = this.MakeAComponentString();
          break;
        case ConstructorStringParserState.Password:
          this.Result.Password = this.MakeAComponentString();
          break;
        case ConstructorStringParserState.Hostname:
          this.Result.Hostname = this.MakeAComponentString();
          break;
        case ConstructorStringParserState.Port:
          this.Result.Port = this.MakeAComponentString();
          break;
        case ConstructorStringParserState.Pathname:
          this.Result.Pathname = this.MakeAComponentString();
          break;
        case ConstructorStringParserState.Search:
          this.Result.Search = this.MakeAComponentString();
          break;
        case ConstructorStringParserState.Hash:
          this.Result.Hash = this.MakeAComponentString();
          break;
        default:
          break;
      };
    }
    this.State = newState;
    this.TokenIndex += skip;
    this.ComponentStart = this.TokenIndex;
    this.TokenIncrement = 0;
  }

  // Ref: https://wicg.github.io/urlpattern/#rewind
  private void Rewind()
  {
    this.TokenIndex = this.ComponentStart;
    this.TokenIncrement = 0;
  }

  // Ref: https://wicg.github.io/urlpattern/#rewind-and-set-state
  private void RewindAndSetState(ConstructorStringParserState state)
  {
    this.Rewind();
    this.State = state;
  }

  // Ref: https://wicg.github.io/urlpattern/#get-a-safe-token
  private Token GetSafeToken(int index)
  {
    if (index < this.TokenList.Count)
    {
      return this.TokenList[index];
    }

    Debug.Assert(this.TokenList.Count >= 1);

    var lastIndex = this.TokenList.Count - 1;
    var token = this.TokenList[lastIndex];

    Debug.Assert(token.Type is TokenType.End);

    return token;
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-non-special-pattern-char
  private bool IsANonSpecialPatternChar(int index, string value)
  {
    var token = this.GetSafeToken(index);

    if (token.Value != value)
    {
      return false;
    }

    if (
      token.Type is TokenType.Char
      || token.Type is TokenType.EscapedChar
      || token.Type is TokenType.InvalidChar
    )
    {
      return true;
    }

    return false;
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-protocol-suffix
  private bool IsAProtocolSuffix()
  {
    return IsANonSpecialPatternChar(this.TokenIndex, ":");
  }

  // Ref: https://wicg.github.io/urlpattern/#next-is-authority-slashes
  private bool NextIsAuthoritySlashes()
  {
    if (this.IsANonSpecialPatternChar(this.TokenIndex + 1, "/") == false) return false;
    if (this.IsANonSpecialPatternChar(this.TokenIndex + 2, "/") == false) return false;
    return true;
  }

  // Ref: https://wicg.github.io/urlpattern/#is-an-identity-terminator
  private bool IsAnIdentityTerminator()
  {
    return IsANonSpecialPatternChar(this.TokenIndex, "@");
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-password-prefix
  private bool IsAPasswordPrefix()
  {
    return IsANonSpecialPatternChar(this.TokenIndex, ":");
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-port-prefix
  private bool IsAPortPrefix()
  {
    return IsANonSpecialPatternChar(this.TokenIndex, ":");
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-pathname-start
  private bool IsAPathnameStart()
  {
    return IsANonSpecialPatternChar(this.TokenIndex, "/");
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-search-prefix
  private bool IsASearchPrefix()
  {
    if (this.IsANonSpecialPatternChar(this.TokenIndex, "?")) return true;
    if (this.TokenList[this.TokenIndex].Value != "?") return false;

    var previousIndex = this.TokenIndex - 1;
    if (previousIndex < 0) return true;

    var previousToken = this.GetSafeToken(previousIndex);

    if (
      previousToken.Type is TokenType.Name ||
      previousToken.Type is TokenType.Regexp ||
      previousToken.Type is TokenType.Close ||
      previousToken.Type is TokenType.Asterisk
    )
    {
      return false;
    }

    return true;
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-hash-prefix
  private bool IsAHashPrefix()
  {
    return IsANonSpecialPatternChar(this.TokenIndex, "#");
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-group-open
  private bool IsAGroupOpen()
  {
    if (this.TokenList[this.TokenIndex].Type is TokenType.Open) return true;
    else return false;
  }

  // Ref: https://wicg.github.io/urlpattern/#is-a-group-close
  private bool IsAGroupClose()
  {
    if (this.TokenList[this.TokenIndex].Type is TokenType.Close) return true;
    else return false;
  }

  // Ref: https://wicg.github.io/urlpattern/#is-an-ipv6-open
  private bool IsAnIPv6Open()
  {
    return this.IsANonSpecialPatternChar(this.TokenIndex, "[");
  }

  // Ref: https://wicg.github.io/urlpattern/#is-an-ipv6-close
  private bool IsAnIPv6Close()
  {
    return this.IsANonSpecialPatternChar(this.TokenIndex, "]");
  }


  // Ref: https://wicg.github.io/urlpattern/#make-a-component-string
  private string MakeAComponentString()
  {
    Debug.Assert(this.TokenIndex < this.TokenList.Count);

    var token = this.TokenList[this.TokenIndex];
    var componentStartToken = this.GetSafeToken(this.ComponentStart);
    var componentStartInputIndex = componentStartToken.Index;
    var endIndex = token.Index;

    return this.Input.Substring(componentStartInputIndex, endIndex - componentStartInputIndex);
  }

  // Ref: https://wicg.github.io/urlpattern/#compute-protocol-matches-a-special-scheme-flag
  private void ComputeProtocolMatchesASpecialSchemeFlag()
  {
    var protocolString = this.MakeAComponentString();
    var protocolComponent = Component.CompileAComponent(protocolString, Canonicalization.CanonicalizeAProtocol, Options.Default);
    if (Internals.ProtocolComponentMatchesASpecialScheme(protocolComponent))
    {
      this.ProtocolMatchesSpecialSchemeFlag = true;
    }
  }
}