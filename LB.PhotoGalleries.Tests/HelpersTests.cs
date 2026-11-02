using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace LB.PhotoGalleries.Tests;

public class HelpersTests
{
    [Fact]
    public void ToScriptJson_EscapesCharactersThatCouldEndTheScriptBlock()
    {
        const string name = "</script><div data-bind=\"text: 1\">'&";

        var json = Helpers.ToScriptJson(new { Name = name }).ToString()!;

        Assert.DoesNotContain("<", json);
        Assert.DoesNotContain(">", json);
        Assert.DoesNotContain("&", json);
        Assert.DoesNotContain("'", json);
        Assert.Equal(name, JsonConvert.DeserializeAnonymousType(json, new { Name = "" })!.Name);
    }

    [Theory]
    [InlineData(25, 25)]
    [InlineData(100, 100)]
    [InlineData(null, Helpers.DefaultAdminPageSize)]
    [InlineData(0, Helpers.DefaultAdminPageSize)]
    [InlineData(-5, Helpers.DefaultAdminPageSize)]
    [InlineData(1000, Helpers.DefaultAdminPageSize)]
    [InlineData(30, Helpers.DefaultAdminPageSize)]
    public void GetAdminPageSize_OnlyAllowsTheOfferedPageSizes(int? requested, int expected)
    {
        Assert.Equal(expected, Helpers.GetAdminPageSize(requested));
    }

    [Theory]
    [InlineData("brands-hatch-2021", "Brands Hatch 2021", true)]
    [InlineData("q&a?", "Q&A?", true)]
    [InlineData("brands-hatch", "Brands Hatch 2021", false)]
    [InlineData("Brands-Hatch-2021", "Brands Hatch 2021", false)]
    [InlineData("anything", "Brands Hatch 2021", false)]
    [InlineData(null, "Brands Hatch 2021", false)]
    public void IsCanonicalUrlName_OnlyMatchesTheEncodedName(string? urlName, string name, bool expected)
    {
        Assert.Equal(expected, Helpers.IsCanonicalUrlName(urlName, name));
    }

    /// <summary>
    /// site.js has its own EncodeParamForUrl() for links built in the browser. These expected values came from running
    /// it, so if this fails the two have drifted apart and browser-built links will be redirected on every click.
    /// </summary>
    [Theory]
    [InlineData("Rd. 3 - Silverstone", "rd.-3-_-silverstone")]
    [InlineData("A  B", "a--b")]
    [InlineData("-lead/trail-", "_lead-trail_")]
    [InlineData("O'Neil (UK) @ #1", "oneil-uk--1")]
    [InlineData("Q&A?", "q&a?")]
    [InlineData("Ducati MotoGP 2020 - Round 8 Catalunya", "ducati-motogp-2020-_-round-8-catalunya")]
    [InlineData("Moto2 / Moto3", "moto2---moto3")]
    public void EncodeParamForUrl_MatchesTheSiteJsVersion(string name, string expected)
    {
        Assert.Equal(expected, Helpers.EncodeParamForUrl(name));
    }

    [Theory]
    [InlineData("https://photos.example.com", "/i/1/2/name")]
    [InlineData("https://photos.example.com/", "/i/1/2/name")]
    [InlineData("https://photos.example.com/", "i/1/2/name")]
    public void GetAbsoluteUrl_JoinsWithOneSlash(string baseUrl, string path)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = baseUrl }).Build();

        Assert.Equal("https://photos.example.com/i/1/2/name", Helpers.GetAbsoluteUrl(config, path));
    }
}
