using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Shared;

namespace LB.PhotoGalleries.Tests.Integration;

/// <summary>
/// Gallery and image search, as used by the site's search page.
/// </summary>
public class SearchQueryTests
{
    /// <summary>
    /// Picks a word from the data to search for, so the tests don't depend on particular content.
    /// </summary>
    private static string PickTerm(IEnumerable<string?> names) =>
        names.SelectMany(n => (n ?? "").Split(' ', '-', '_'))
            .Where(w => w.Length >= 4 && w.All(char.IsAsciiLetter))
            .GroupBy(w => w.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .First().Key;

    [CosmosFact]
    public async Task SearchForGalleriesAsync_FindsActiveGalleriesByNameNewestFirst()
    {
        var active = (await CosmosTestDatabase.GetGalleriesAsync()).Where(g => g.Active).ToList();
        var term = PickTerm(active.Select(g => g.Name));
        var terms = SearchTerms.Expand(term);
        var expected = active.Where(g => terms.Any(t => g.Name?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        var actual = await Server.Instance.Galleries.SearchForGalleriesAsync(term.ToUpperInvariant(), searchStatus: SearchStatus.Active, pageSize: 100);

        Assert.Equal(expected.Count, actual.TotalResults);
        Assert.Equal(expected.OrderByDescending(g => g.Created).Take(100).Select(g => g.Id), actual.Results.Select(g => g.Id));
    }

    [CosmosFact]
    public async Task SearchForImagesAsync_FindsImagesByNameOrTagNewestFirst()
    {
        var images = await CosmosTestDatabase.GetImagesAsync();
        var term = PickTerm(images.Select(i => i.Name).Concat(images.SelectMany(i => (i.TagsCsv ?? "").Split(','))));
        var terms = SearchTerms.Expand(term);
        var expected = images
            .Where(i => terms.Any(t => (i.Name?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                       (i.TagsCsv?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToList();

        var actual = await Server.Instance.Images.SearchForImagesAsync(term.ToUpperInvariant(), pageSize: 100, includeInactiveGalleries: true);

        // results are capped at 500
        Assert.Equal(Math.Min(expected.Count, 500), actual.TotalResults);
        var expectedIds = expected.Select(i => i.Id).ToHashSet();
        Assert.All(actual.Results, i => Assert.Contains(i.Id, expectedIds));

        // the page should keep the database's newest-first order. compare with cosmos's own ORDER BY rather than sorting
        // here: the dev data has a pair of images, uploaded seconds apart, that cosmos orders the other way round.
        var termsClause = string.Join(" OR ", terms.Select(t => $"CONTAINS(i.Name, '{t}', true) OR CONTAINS(i.TagsCsv, '{t}', true)"));
        var ordered = await CosmosTestDatabase.QueryAsync<ImageRow>("Images",
            $"SELECT TOP 100 i.id FROM i WHERE {termsClause} ORDER BY i.Created DESC");
        Assert.Equal(ordered.Select(i => i.Id), actual.Results.Select(i => i.Id));
    }
}
