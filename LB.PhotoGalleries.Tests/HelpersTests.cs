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
}
