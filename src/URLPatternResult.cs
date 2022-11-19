// Ref: https://wicg.github.io/urlpattern/#dictdef-urlpatternresult
public record URLPatternResult(
  IEnumerable<object> Inputs,

  URLPatternComponentResult Protocol,
  URLPatternComponentResult Username,
  URLPatternComponentResult Password,
  URLPatternComponentResult Hostname,
  URLPatternComponentResult Port,
  URLPatternComponentResult Pathname,
  URLPatternComponentResult Search,
  URLPatternComponentResult Hash
);

// Ref: https://wicg.github.io/urlpattern/#dictdef-urlpatterncomponentresult
public record URLPatternComponentResult(
  string Input,
  Dictionary<string, string?> Groups
);