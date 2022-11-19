using Xunit;
using URLPatternWebApi;
using System.Collections.Generic;

namespace URLPatternWebApi.Test;

public class URLPatternTest
{

  [Fact]
  public void URLPattern_ReadmeExample()
  {
    // Create the URLPattern to match against.
    var init = new URLPatternInit
    {
      Pathname = "/users/:id"
    };
    var pattern = new URLPattern(init);

    // Match the pattern against a URL.
    var url = "https://example.com/users/123";
    var result = pattern.Exec(url);
    Assert.Equal("123", result.Pathname.Groups["id"]);
  }

  [Fact]
  public void URLPattern_StringWithoutBaseURL()
  {
    var pattern = new URLPattern("https://example.com/users/:id");

    Assert.Equal("https", pattern.Protocol);
    Assert.Equal("", pattern.Username);
    Assert.Equal("", pattern.Password);
    Assert.Equal(@"example\.com", pattern.Hostname);
    Assert.Equal(@"\/users\/:id([^\/]+?)", pattern.Pathname);
    Assert.Equal("", pattern.Port);
    Assert.Equal("", pattern.Search);
    Assert.Equal("", pattern.Hash);

    var result = pattern.Exec("https://example.com/users/123");

    Assert.Equal("123", result.Pathname.Groups["id"]);
  }

  [Fact]
  public void URLPattern_StripsDefaultPorts()
  {
    URLPattern pattern;
    pattern = new URLPattern("ftp://example.com:21");
    Assert.Equal("", pattern.Port);
    pattern = new URLPattern("file://example.com");
    Assert.Equal("", pattern.Port);
    pattern = new URLPattern("http://example.com:80");
    Assert.Equal("", pattern.Port);
    pattern = new URLPattern("https://example.com:443");
    Assert.Equal("", pattern.Port);
    pattern = new URLPattern("ws://example.com:80");
    Assert.Equal("", pattern.Port);
    pattern = new URLPattern("wss://example.com:443");
    Assert.Equal("", pattern.Port);
  }

  [Fact]
  public void URLPattern_StringWithBaseURL()
  {
    var pattern = new URLPattern("/users/:id", "https://example.com");
    var result = pattern.Exec("https://example.com/users/123");
    Assert.Equal("123", result.Pathname.Groups["id"]);
  }

  [Fact]
  public void URLPattern_FilterOnASpecificURLComponent()
  {
    var pattern = new URLPattern(new URLPatternInit
    {
      Hostname = "example.com",
      Pathname = "/foo/*"
    });

    var result = pattern.Exec("/foo/bar", "https://example.com/baz");

    Assert.Equal("/foo/bar", result.Pathname.Input);
    Assert.Equal("bar", result.Pathname.Groups["0"]);
    Assert.Equal("example.com", result.Hostname.Input);
  }

  // From MDN

  [Fact]
  public void FixedTextAndCaputreGroups()
  {
    // A pattern matching some fixed text
    var pattern = new URLPattern(new URLPatternInit { Pathname = "/books" });
    Assert.True(pattern.Test("https://example.com/books"));
    Assert.Empty(pattern.Exec("https://example.com/books").Pathname.Groups);

    // A pattern matching with a named group
    pattern = new URLPattern(new URLPatternInit { Pathname = "/books/:id" });
    Assert.True(pattern.Test("https://example.com/books/123"));
    Assert.Equal(new Dictionary<string, string>() { { "id", "123" } }, pattern.Exec("https://example.com/books/123").Pathname.Groups);
  }


  [Fact]
  public void RegexMatchers()
  {
    var pattern = new URLPattern("/books/:id(\\d+)", "https://example.com");
    Assert.True(pattern.Test("https://example.com/books/123"));
    Assert.False(pattern.Test("https://example.com/books/abc"));
    Assert.False(pattern.Test("https://example.com/books/"));
  }

  // [Fact]
  // public void RegexMatchersLimitations()
  // {
  //   var pattern = new URLPattern();
  //   Assert.True(pattern.Test("https://example.com/books/123"));
  //   Assert.False(pattern.Test("https://example.com/books/abc"));
  //   Assert.False(pattern.Test("https://example.com/books/"));
  // }
}