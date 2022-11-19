public class Utils
{
  public static bool IsAsciiString(string value)
  {
    foreach (var codePoint in value)
    {
      if (IsAscii(codePoint) == false)
      {
        return false;
      }
    }
    return true;
  }

  public static bool IsAsciiDigit(char codePoint) => codePoint >= 48 && codePoint <= 57;

  public static bool IsAscii(char codePoint) => codePoint >= 0 && codePoint <= 127;

  public static IList<int> GetTheIndices<T>(IList<T> list) => list.Select((_, index) => index).ToList();

  public static bool IsAValidNameCodePoint(char codePoint, bool first)
  {
    if (first)
    {
      return
          (codePoint >= 65 && codePoint <= 90) || // `A-Z`
          (codePoint >= 97 && codePoint <= 122) || // `a-z`
          codePoint == 95; // `_`
    }

    return (codePoint >= 48 && codePoint <= 57) || // `0-9`
        (codePoint >= 65 && codePoint <= 90) || // `A-Z`
        (codePoint >= 97 && codePoint <= 122) || // `a-z`
        codePoint == 95; // `_`
  }

  public static bool IsSpecialScheme(string? input)
  {
    if (input is "ftp" or "file" or "http" or "https" or "ws" or "wss") return true;
    return false;
  }

  public static bool IsSpecialSchemeDefaultPort(string? scheme, string? port)
  {
    if (IsSpecialScheme(scheme) is false) return false;
    if (scheme is "ftp" && port is "21") return true;
    if (scheme is "file" && port is null) return true;
    if (scheme is "http" && port is "80") return true;
    if (scheme is "https" && port is "443") return true;
    if (scheme is "ws" && port is "80") return true;
    if (scheme is "wss" && port is "443") return true;
    return false;
  }
}