## URLPattern

This package implements the [`URLPattern` web API](https://developer.mozilla.org/en-US/docs/Web/API/URL_Pattern_API) in C#. We aim to follow the specification as closely as possible.

### Example

```csharp
using URLPatternWebApi;

// Create the URLPattern to match against.
var init = new URLPatternInit {
  Pathname = "/users/:id"
};
var pattern = new URLPattern(init);

// Match the pattern against a URL.
var url = "https://example.com/users/123";
var result = pattern.Exec(url);
Assert.Equal("123", result.Pathname.Groups["id"]);
```

### Tests

To execute the tests, run:
```bash
cd ./test
dotnet watch test
```