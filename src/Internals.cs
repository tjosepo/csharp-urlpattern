using System.Text.RegularExpressions;

namespace URLPatternWebApi;

internal static class Internals
{
  // Ref: https://wicg.github.io/urlpattern/#protocol-component-matches-a-special-scheme
  internal static bool ProtocolComponentMatchesASpecialScheme(Component protocolComponent)
  {
    var specialSchemeList = new List<string>() { "ftp", "file", "http", "https", "ws", "wss" };
    foreach (var scheme in specialSchemeList)
    {
      var testResult = protocolComponent.RegularExpression.IsMatch(scheme);
      if (testResult)
      {
        return true;
      }
    }
    return false;
  }

  // Ref: https://wicg.github.io/urlpattern/#hostname-pattern-is-an-ipv6-address
  internal static bool HostnamePatternIsAnIPv6Address(string? input)
  {
    if (input is null || input.Length < 2) return false;
    var inputCodePoints = input.ToCharArray();
    if (inputCodePoints[0] is '[') return true;
    if (inputCodePoints[0] is '{' && inputCodePoints[1] is '[') return true;
    if (inputCodePoints[0] is '\\' && inputCodePoints[1] is '[') return true;
    return false;
  }

  // Ref: https://wicg.github.io/urlpattern/#match
  internal static URLPatternResult? Match(URLPattern urlpattern, object input, string? baseURLString)
  {
    var protocol = string.Empty;
    var username = string.Empty;
    var password = string.Empty;
    var hostname = string.Empty;
    var port = string.Empty;
    var pathname = string.Empty;
    var search = string.Empty;
    var hash = string.Empty;
    var inputs = new List<object>();

    if (input is URLPatternInit)
    {
      if (baseURLString is not null) throw new TypeError();
      try
      {
        var applyResult = URLPattern.ProcessAURLPatternInit((URLPatternInit)input, "url", protocol, username, password, hostname, port, pathname, search, hash);
        protocol = applyResult.Protocol;
        username = applyResult.Username;
        password = applyResult.Password;
        hostname = applyResult.Hostname;
        port = applyResult.Port;
        pathname = applyResult.Pathname;
        search = applyResult.Search;
        hash = applyResult.Hash;
      }
      catch
      {
        return null;
      }
    }
    else
    {
      Uri? baseURL = null;

      if (baseURLString is not null)
      {
        try
        {
          baseURL = new Uri(baseURLString);
        }
        catch
        {
          return null;
        }
        inputs.Add(baseURLString);
      }

      try
      {
        // var url = new UriBuilder((baseURL?.Host ?? "") + (string)input);
        var url = new UriBuilder((baseURL?.Host ?? "") + (string)input);
        protocol = url.Scheme;
        username = url.UserName;
        password = url.Password;
        hostname = url.Host;
        port = Utils.IsSpecialSchemeDefaultPort(url.Scheme, url.Port.ToString()) ? "" : url.Port.ToString();
        pathname = url.Path;
        search = url.Query;
        hash = url.Fragment;
      }
      catch
      {
        return null;
      }
    }

    var protocolExecResult = urlpattern.ProtocolComponent.RegularExpression.Match(protocol!);
    var usernameExecResult = urlpattern.UsernameComponent.RegularExpression.Match(username!);
    var passwordExecResult = urlpattern.PasswordComponent.RegularExpression.Match(password!);
    var hostnameExecResult = urlpattern.HostnameComponent.RegularExpression.Match(hostname!);
    var portExecResult = urlpattern.PortComponent.RegularExpression.Match(port!);
    var pathnameExecResult = urlpattern.PathnameComponent.RegularExpression.Match(pathname!);
    var searchExecResult = urlpattern.SearchComponent.RegularExpression.Match(search!);
    var hashExecResult = urlpattern.HashComponent.RegularExpression.Match(hash!);

    if (
      protocolExecResult.Success is false ||
      usernameExecResult.Success is false ||
      passwordExecResult.Success is false ||
      hostnameExecResult.Success is false ||
      portExecResult.Success is false ||
      pathnameExecResult.Success is false ||
      searchExecResult.Success is false ||
      hashExecResult.Success is false
    )
    {
      return null;
    }

    var result = new URLPatternResult(
      inputs,
      CreateAComponentMatchResult(urlpattern.ProtocolComponent, protocol!, protocolExecResult),
      CreateAComponentMatchResult(urlpattern.UsernameComponent, username!, usernameExecResult),
      CreateAComponentMatchResult(urlpattern.PasswordComponent, password!, passwordExecResult),
      CreateAComponentMatchResult(urlpattern.HostnameComponent, hostname!, hostnameExecResult),
      CreateAComponentMatchResult(urlpattern.PortComponent, port!, portExecResult),
      CreateAComponentMatchResult(urlpattern.PathnameComponent, pathname!, pathnameExecResult),
      CreateAComponentMatchResult(urlpattern.SearchComponent, search!, searchExecResult),
      CreateAComponentMatchResult(urlpattern.HashComponent, hash!, hashExecResult)
    );

    return result;
  }

  // Ref: https://wicg.github.io/urlpattern/#create-a-component-match-result
  internal static URLPatternComponentResult CreateAComponentMatchResult(Component component, string input, Match execResult)
  {
    var groups = new Dictionary<string, string?>();
    var index = 1;
    while (index < execResult.Groups.Count)
    {
      var name = component.GroupNameList[index - 1];
      var value = execResult.Groups[index].Value;
      groups.Add(name, value);
      index += 1;
    }

    var result = new URLPatternComponentResult(
      input,
      groups
    );

    return result;
  }
}