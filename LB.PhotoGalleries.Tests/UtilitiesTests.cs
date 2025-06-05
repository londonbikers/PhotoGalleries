using LB.PhotoGalleries.Shared;

namespace LB.PhotoGalleries.Tests;

public class UtilitiesTests
{
    [Theory]
    [InlineData(null, "foo", "foo")]
    [InlineData("", "foo", "foo")]
    public void AddTagToCsv_ReturnsTagWhenListIsNullOrEmpty(string existing, string tag, string expected)
    {
        var result = Utilities.AddTagToCsv(existing, tag);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AddTagToCsv_AppendsTrimmedLowerTag()
    {
        var result = Utilities.AddTagToCsv("one", " Two ");
        Assert.Equal("one,two", result);
    }

    [Fact]
    public void AddTagToCsv_DoesNotAddDuplicateIgnoringCase()
    {
        var original = "one,two";
        var result = Utilities.AddTagToCsv(original, " TWO ");
        Assert.Equal(original, result);
    }

    [Fact]
    public void RemoveTagFromCsv_RemovesTagIgnoringCase()
    {
        var result = Utilities.RemoveTagFromCsv("one,two,three", " TWO ");
        Assert.Equal("one,three", result);
    }

    [Fact]
    public void RemoveTagFromCsv_ReturnsNullWhenEmpty()
    {
        Assert.Null(Utilities.RemoveTagFromCsv(null, "foo"));
        Assert.Null(Utilities.RemoveTagFromCsv("", "foo"));
    }
}
