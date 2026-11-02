using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;

namespace LB.PhotoGalleries.Tests.Integration;

/// <summary>
/// Comments are stored as an array on galleries and images, so querying them needs JOIN or EXISTS. These queries
/// silently matched nothing for years (see #93), so check them against the raw data.
/// </summary>
public class CommentQueryTests
{
    private static async Task<Dictionary<string, int>> GetExpectedCommentCountsAsync()
    {
        var galleries = await CosmosTestDatabase.GetGalleriesAsync();
        var images = await CosmosTestDatabase.GetImagesAsync();
        return galleries.SelectMany(g => g.Comments ?? new())
            .Concat(images.SelectMany(i => i.Comments ?? new()))
            .GroupBy(c => c.CreatedByUserId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    [CosmosFact]
    public async Task GetUserCommentCountsAsync_CountsEveryUsersComments()
    {
        var expected = await GetExpectedCommentCountsAsync();
        Assert.NotEmpty(expected);

        var actual = await Server.Instance.Users.GetUserCommentCountsAsync();

        Assert.Equal(expected.OrderBy(kv => kv.Key), actual.OrderBy(kv => kv.Key));
    }

    [CosmosFact]
    public async Task GetUserCommentCountsAsync_OnlyReturnsTheRequestedUsers()
    {
        var expected = await GetExpectedCommentCountsAsync();
        var requested = expected.Keys.Take(2).Append("no-such-user").ToList();

        var actual = await Server.Instance.Users.GetUserCommentCountsAsync(requested);

        Assert.Equal(expected.Where(kv => requested.Contains(kv.Key)).OrderBy(kv => kv.Key), actual.OrderBy(kv => kv.Key));
    }

    [CosmosFact]
    public async Task GetUserCommentCountAsync_CountsOneUsersComments()
    {
        var (userId, count) = (await GetExpectedCommentCountsAsync()).MaxBy(kv => kv.Value);

        Assert.Equal(count, await Server.Instance.Users.GetUserCommentCountAsync(new User { Id = userId }));
        Assert.Equal(0, await Server.Instance.Users.GetUserCommentCountAsync(new User { Id = "no-such-user" }));
    }

    [CosmosFact]
    public async Task GetImagesUserHasCommentedOnAsync_FindsEveryImageTheUserCommentedOn()
    {
        var images = await CosmosTestDatabase.GetImagesAsync();
        var userId = images.SelectMany(i => i.Comments ?? new()).GroupBy(c => c.CreatedByUserId).MaxBy(g => g.Count())?.Key;
        Assert.NotNull(userId);
        var expected = images.Where(i => i.Comments?.Any(c => c.CreatedByUserId == userId) == true).Select(i => i.Id).ToHashSet();

        var actual = await Server.Instance.Images.GetImagesUserHasCommentedOnAsync(userId);

        Assert.Equal(expected, actual.Select(i => i.Id).ToHashSet());
        Assert.Empty(await Server.Instance.Images.GetImagesUserHasCommentedOnAsync("no-such-user"));
    }

    [CosmosFact]
    public async Task GetGalleriesUserIsInvolvedInAsync_FindsGalleriesTheUserCreatedOrCommentedOn()
    {
        var galleries = await CosmosTestDatabase.GetGalleriesAsync();

        // prefer someone who commented on a gallery they didn't create, as that's the case the old query missed
        var userId = galleries
            .SelectMany(g => (g.Comments ?? new()).Where(c => c.CreatedByUserId != g.CreatedByUserId).Select(c => c.CreatedByUserId))
            .FirstOrDefault() ?? galleries.Select(g => g.CreatedByUserId).FirstOrDefault(id => id != null);
        Assert.NotNull(userId);
        var expected = galleries
            .Where(g => g.CreatedByUserId == userId || g.Comments?.Any(c => c.CreatedByUserId == userId) == true)
            .Select(g => g.Id)
            .ToHashSet();

        var actual = await Server.Instance.Galleries.GetGalleriesUserIsInvolvedInAsync(userId);

        Assert.Equal(expected, actual.Select(g => g.Id).ToHashSet());
    }
}
