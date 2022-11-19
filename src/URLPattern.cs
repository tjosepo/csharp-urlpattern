using System.Diagnostics;

namespace URLPatternWebApi;

public class URLPattern
{
  internal Component ProtocolComponent { get; set; }
  internal Component UsernameComponent { get; set; }
  internal Component PasswordComponent { get; set; }
  internal Component HostnameComponent { get; set; }
  internal Component PortComponent { get; set; }
  internal Component PathnameComponent { get; set; }
  internal Component SearchComponent { get; set; }
  internal Component HashComponent { get; set; }

  public string Protocol { get => this.ProtocolComponent.PatternString; }
  public string Username { get => this.UsernameComponent.PatternString; }
  public string Password { get => this.PasswordComponent.PatternString; }
  public string Hostname { get => this.HostnameComponent.PatternString; }
  public string Port { get => this.PortComponent.PatternString; }
  public string Pathname { get => this.PathnameComponent.PatternString; }
  public string Search { get => this.SearchComponent.PatternString; }
  public string Hash { get => this.HashComponent.PatternString; }

  public bool Test(string input, string? baseURL = null)
  {
    var result = Internals.Match(this, input, baseURL);
    if (result is null) return false;
    return true;
  }

  public URLPatternResult? Exec(string input, string? baseURL = null)
  {
    return Internals.Match(this, input, baseURL);
  }

  public URLPattern(URLPatternInit input, URLPatternOptions? options = null)
  {
    Initialize(input, null, options);
  }

  public URLPattern(string input, string? baseURL = null, URLPatternOptions? options = null)
  {
    Initialize(input, baseURL, options);
  }

  // Ref: https://wicg.github.io/urlpattern/#urlpattern-initialize
  private void Initialize(object input, string? baseURL, URLPatternOptions? options)
  {
    URLPatternInit? init = null;

    if (input is string)
    {
      init = ConstructorStringParser.ParseAConstructorString((string)input);
      if (baseURL is null && init.Protocol is null) throw new Exception("TypeError");
      init.baseURL = baseURL;
    }
    else
    {
      Debug.Assert(input is URLPatternInit);
      if (baseURL is not null) throw new Exception("TypeError");
      init = (URLPatternInit)input;
    }

    var processedInit = ProcessAURLPatternInit(init, "pattern", null, null, null, null, null, null, null, null);
    if (Utils.IsSpecialScheme(processedInit.Protocol) &&
    Utils.IsSpecialSchemeDefaultPort(processedInit.Protocol, processedInit.Port))
    {
      processedInit.Port = string.Empty;
    }

    this.ProtocolComponent = Component.CompileAComponent(processedInit.Protocol, Canonicalization.CanonicalizeAProtocol, Options.Default);
    this.UsernameComponent = Component.CompileAComponent(processedInit.Username, Canonicalization.CanonicalizeAUserName, Options.Default);
    this.PasswordComponent = Component.CompileAComponent(processedInit.Password, Canonicalization.CanonicalizeAPassword, Options.Default);

    if (Internals.HostnamePatternIsAnIPv6Address(processedInit.Hostname))
    {
      this.HostnameComponent = Component.CompileAComponent(processedInit.Hostname, Canonicalization.CanonicalizeAnIPv6Hostname, Options.Hostname);
    }
    else
    {
      this.HostnameComponent = Component.CompileAComponent(processedInit.Hostname, Canonicalization.CanonicalizeAHostname, Options.Hostname);
    }

    this.PortComponent = Component.CompileAComponent(processedInit.Port, Canonicalization.CanonicalizeAPort, Options.Default);

    var compileOptions = new Options(Options.Default) { ignoreCase = options?.ignoreCase ?? false };

    if (Internals.ProtocolComponentMatchesASpecialScheme(this.ProtocolComponent))
    {
      var pathCompileOptions = new Options(Options.Pathname) { ignoreCase = options?.ignoreCase ?? false };
      this.PathnameComponent = Component.CompileAComponent(processedInit.Pathname, Canonicalization.CanonicalizeAPathname, pathCompileOptions);
    }
    else
    {
      this.PasswordComponent = Component.CompileAComponent(processedInit.Pathname, Canonicalization.CanonicalizeAnOpaquePathname, compileOptions);
    }

    this.SearchComponent = Component.CompileAComponent(processedInit.Search, Canonicalization.CanonicalizeASearch, compileOptions);
    this.HashComponent = Component.CompileAComponent(processedInit.Hash, Canonicalization.CanonicalizeAHash, compileOptions);

  }

  // Ref: https://wicg.github.io/urlpattern/#process-a-urlpatterninit
  internal static URLPatternInit ProcessAURLPatternInit(URLPatternInit init, string type, string? protocol, string? username, string? password, string? hostname, string? port, string? pathname, string? search, string? hash)
  {
    var result = new URLPatternInit();
    result.Protocol = protocol;
    result.Username = username;
    result.Password = password;
    result.Hostname = hostname;
    result.Port = port;
    result.Pathname = pathname;
    result.Search = search;
    result.Hash = hash;
    UriBuilder? baseURL = null;
    if (init.baseURL is not null)
    {
      try
      {
        baseURL = new UriBuilder(init.baseURL);
      }
      catch (Exception)
      {
        throw new Exception("TypeError");
      }
      result.Protocol = ProcessABaseURLString(baseURL.Scheme, type);
      result.Username = ProcessABaseURLString(baseURL.UserName, type);
      result.Password = ProcessABaseURLString(baseURL.Password, type);
      result.Hostname = ProcessABaseURLString(baseURL.Host, type);
      result.Port = ProcessABaseURLString(baseURL.Port.ToString(), type);
      result.Pathname = ProcessABaseURLString(baseURL.Path, type);
      result.Search = ProcessABaseURLString(baseURL.Query, type);
      result.Hash = ProcessABaseURLString(baseURL.Fragment, type);
    }

    if (init.Protocol is not null) result.Protocol = ProcessProtocolForInit(init.Protocol, type);
    if (init.Username is not null) result.Username = ProcessUsernameForInit(init.Username, type);
    if (init.Password is not null) result.Password = ProcessPasswordForInit(init.Password, type);
    if (init.Hostname is not null) result.Hostname = ProcessHostnameForInit(init.Hostname, type);
    if (init.Port is not null) result.Port = ProcessPortForInit(init.Port, result.Protocol, type);
    if (init.Pathname is not null)
    {
      result.Pathname = init.Pathname;
      if (baseURL is not null &&
        // TODO: not sure about opaque
        baseURL.Uri.IsAbsoluteUri &&
        IsAnAbsolutePathname(result.Pathname, type) is false)
      {
        var baseURLPath = ProcessABaseURLString(baseURL.ToString(), type);
        var slashIndex = baseURLPath.LastIndexOf('/');
        if (slashIndex is not -1)
        {
          var newPathname = baseURLPath.Substring(0, slashIndex + 1);
          newPathname += result.Pathname;
          result.Pathname = newPathname;
        }
      }

      result.Pathname = ProcessPathnameForInit(result.Pathname, result.Protocol, type);
    }

    if (init.Search is not null) result.Search = ProcessSearchForInit(init.Search, type);
    if (init.Hash is not null) result.Hash = ProcessSearchForInit(init.Hash, type);

    return result;
  }

  // Ref: https://wicg.github.io/urlpattern/#process-a-base-url-string
  private static string ProcessABaseURLString(string input, string type)
  {
    Debug.Assert(input is not null);
    if (type != "pattern") return input;
    return Component.EscapeAPatternString(input);
  }

  // Ref: https://wicg.github.io/urlpattern/#process-a-base-url-string
  private static bool IsAnAbsolutePathname(string input, string type)
  {
    if (input == string.Empty) return false;
    if (input[0] is '/') return true;
    if (type is "url") return false;
    if (input.Length < 2) return false;
    if (input[0] is '\\' && input[1] is '/') return true;
    if (input[0] is '{' && input[1] is '/') return true;
    return false;
  }

  // Ref: https://wicg.github.io/urlpattern/#process-protocol-for-init
  private static string ProcessProtocolForInit(string value, string type)
  {
    var strippedValue = value.EndsWith(':') ? value.Substring(0, value.Length - 1) : value;
    if (type is "pattern") return strippedValue;
    return Canonicalization.CanonicalizeAProtocol(strippedValue);
  }

  // Ref: https://wicg.github.io/urlpattern/#process-username-for-init
  private static string ProcessUsernameForInit(string value, string type)
  {
    if (type is "pattern") return value;
    return Canonicalization.CanonicalizeAUserName(value);
  }

  // Ref: https://wicg.github.io/urlpattern/#process-password-for-init
  private static string ProcessPasswordForInit(string value, string type)
  {
    if (type is "pattern") return value;
    return Canonicalization.CanonicalizeAPassword(value);
  }

  // Ref: https://wicg.github.io/urlpattern/#process-hostname-for-init
  private static string ProcessHostnameForInit(string value, string type)
  {
    if (type is "pattern") return value;
    return Canonicalization.CanonicalizeAHostname(value);
  }

  // Ref: https://wicg.github.io/urlpattern/#process-port-for-init
  private static string ProcessPortForInit(string portValue, string? protocolValue, string type)
  {
    if (type is "pattern") return portValue;
    return Canonicalization.CanonicalizeAPort(portValue, protocolValue);
  }

  // Ref: https://wicg.github.io/urlpattern/#process-pathname-for-init
  private static string ProcessPathnameForInit(string pathnameValue, string? protocolValue, string type)
  {
    if (type is "pattern") return pathnameValue;
    if (Utils.IsSpecialScheme(protocolValue) || protocolValue == string.Empty) return Canonicalization.CanonicalizeAPathname(pathnameValue);
    return Canonicalization.CanonicalizeAnOpaquePathname(pathnameValue);
  }

  // Ref: https://wicg.github.io/urlpattern/#process-search-for-init
  private static string ProcessSearchForInit(string value, string type)
  {
    var strippedValue = value.StartsWith('?') ? value.Substring(1) : value;
    if (type is "pattern") return strippedValue;
    return Canonicalization.CanonicalizeASearch(strippedValue);
  }

  // Ref: https://wicg.github.io/urlpattern/#process-hash-for-init
  private static string ProcessHashForInit(string value, string type)
  {
    var strippedValue = value.StartsWith('#') ? value.Substring(1) : value;
    if (type is "pattern") return strippedValue;
    return Canonicalization.CanonicalizeAHash(strippedValue);
  }


}

public record URLPatternInit
{
  public string? Protocol;
  public string? Username;
  public string? Password;
  public string? Hostname;
  public string? Port;
  public string? Pathname;
  public string? Search;
  public string? Hash;
  public string? baseURL;
}

public record URLPatternOptions
{
  public bool ignoreCase = false;
}
