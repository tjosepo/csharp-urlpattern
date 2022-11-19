using System.Diagnostics;

static public class Canonicalization
{
  // Ref: https://wicg.github.io/urlpattern/#canonicalize-a-protocol
  static public string CanonicalizeAProtocol(string value)
  {
    if (value == string.Empty)
    {
      return value;
    }

    return new Uri(value + "://dummy.test").Scheme;
  }

  // Ref: https://wicg.github.io/urlpattern/#canonicalize-a-username
  static public string CanonicalizeAUserName(string value)
  {
    if (value == string.Empty)
    {
      return value;
    }

    var dummyURL = new UriBuilder("http://dummy.test");
    dummyURL.UserName = value;
    return dummyURL.UserName;
  }

  // Ref: https://wicg.github.io/urlpattern/#canonicalize-a-password
  static public string CanonicalizeAPassword(string value)
  {
    if (value == string.Empty)
    {
      return value;
    }

    var dummyURL = new UriBuilder("http://dummy.test");
    dummyURL.Password = value;
    return dummyURL.Password;
  }

  // Ref: https://wicg.github.io/urlpattern/#canonicalize-a-hostname
  static public string CanonicalizeAHostname(string value)
  {
    if (value == string.Empty)
    {
      return value;
    }

    var dummyURL = new UriBuilder("http://" + value);
    dummyURL.Host = value;
    return dummyURL.Host;
  }

  // Ref: https://wicg.github.io/urlpattern/#canonicalize-an-ipv6-hostname
  static public string CanonicalizeAnIPv6Hostname(string value)
  {
    var result = string.Empty;

    foreach (char codePoint in value)
    {
      if ((codePoint >= 65 && codePoint <= 90) is false  // `A-Z`
          && (codePoint >= 97 && codePoint <= 122) is false // `a-z`
          && codePoint is not '['
          && codePoint is not ']'
          && codePoint is not ':')
      {
        throw new Exception("TypeError");
      }

      result += Char.ToLower(codePoint);
    }

    return result;
  }

  // Ref: https://wicg.github.io/urlpattern/#canonicalize-a-port
  static public string CanonicalizeAPort(string value) => CanonicalizeAPort(value, null);
  static public string CanonicalizeAPort(string value, string? protocolValue = null)
  {
    if (value == string.Empty)
    {
      return value;
    }

    var dummyURL = new UriBuilder("http://dummy.test");

    if (protocolValue != null)
    {
      dummyURL.Scheme = protocolValue;
    }

    dummyURL.Port = Int32.Parse(value);
    var isDefaultPort = dummyURL.Port == -1;
    return isDefaultPort ? string.Empty : dummyURL.Port.ToString();
  }

  // Ref: https://wicg.github.io/urlpattern/#canonicalize-a-pathname
  static public string CanonicalizeAPathname(string value)
  {
    if (value == string.Empty)
    {
      return value;
    }

    var leadingSlash = (value.First() == '/');
    var modifiedValue = leadingSlash ? string.Empty : "/-";
    modifiedValue += value;

    var dummyURL = new UriBuilder("http://dummy.test");
    dummyURL.Path = modifiedValue;
    var result = dummyURL.Path;

    if (!leadingSlash)
    {
      result = result.Substring(2);
    }

    return result;
  }

  // Ref: https://wicg.github.io/urlpattern/#canonicalize-an-opaque-pathname
  static public string CanonicalizeAnOpaquePathname(string value)
  {
    if (value == string.Empty)
    {
      return value;
    }

    var dummyURL = new UriBuilder("data://dummy,test");
    dummyURL.Path = value;
    return dummyURL.Path;
  }

  // Ref: https://wicg.github.io/urlpattern/#canonicalize-a-search
  static public string CanonicalizeASearch(string value)
  {
    if (value == string.Empty)
    {
      return value;
    }

    var dummyURL = new UriBuilder("http://dummy.test");
    dummyURL.Query = value;
    return dummyURL.Query;
  }

  // Ref: https://wicg.github.io/urlpattern/#canonicalize-a-hash
  static public string CanonicalizeAHash(string value)
  {
    if (value == string.Empty)
    {
      return value;
    }

    var dummyURL = new UriBuilder("http://dummy.test");
    dummyURL.Fragment = value;
    return dummyURL.Fragment;
  }
}