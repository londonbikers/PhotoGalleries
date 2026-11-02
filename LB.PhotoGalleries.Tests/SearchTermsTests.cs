using LB.PhotoGalleries.Shared;

namespace LB.PhotoGalleries.Tests;

public class SearchTermsTests
{
    [Theory]
    [InlineData("cars", "car")]
    [InlineData("bikes", "bike")]
    [InlineData("boxes", "box")]
    [InlineData("crashes", "crash")]
    [InlineData("bodies", "body")]
    [InlineData("glass", "glass")]
    [InlineData("chassis", "chassis")]
    [InlineData("series", "series")]
    [InlineData("abs", "abs")]
    [InlineData("ducati", "ducati")]
    public void Singularise_ReturnsSingular(string word, string expected)
    {
        Assert.Equal(expected, SearchTerms.Singularise(word));
    }

    [Fact]
    public void Expand_SearchesForSingularOnly()
    {
        Assert.Equal(["car"], SearchTerms.Expand(" Cars "));
    }

    [Fact]
    public void Expand_AddsSynonyms()
    {
        Assert.Equal(["kwak", "kawasaki", "kawi"], SearchTerms.Expand("Kwak"));
        Assert.Equal(["trumpet", "triumph"], SearchTerms.Expand("trumpets"));
        Assert.Equal(["motorcycle", "bike", "motorbike"], SearchTerms.Expand("motorcycles"));
    }

    [Fact]
    public void Expand_AddsSynonymsOfWholePhrase()
    {
        Assert.Equal(["track day", "trackday"], SearchTerms.Expand("track days"));
    }

    [Fact]
    public void Expand_AddsSynonymsOfLastWord()
    {
        Assert.Equal(["red bike", "red motorbike", "red motorcycle"], SearchTerms.Expand("red  bikes"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Expand_ReturnsNothingForEmptyQuery(string? query)
    {
        Assert.Empty(SearchTerms.Expand(query));
    }
}
