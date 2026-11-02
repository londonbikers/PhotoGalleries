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
}
