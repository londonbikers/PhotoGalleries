using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;

namespace LB.PhotoGalleries.Tests.Integration;

/// <summary>
/// The admin comments list: comments from galleries and images merged together, paged, searched and filtered by user.
/// </summary>
public class AdminCommentQueryTests
{
    private static async Task<List<(string GalleryOrImageId, CommentRow Comment)>> GetExpectedCommentsAsync()
    {
        var galleries = await CosmosTestDatabase.GetGalleriesAsync();
        var images = await CosmosTestDatabase.GetImagesAsync();
        return galleries.SelectMany(g => (g.Comments ?? new()).Select(c => (g.Id, c)))
            .Concat(images.SelectMany(i => (i.Comments ?? new()).Select(c => (i.Id, c))))
            .ToList();
    }

    private static string Key(string galleryOrImageId, DateTime created, string userId) => $"{galleryOrImageId}|{created.Ticks}|{userId}";

    private static string Key(CommentAdminStub comment) => Key(comment.ImageId ?? comment.GalleryId, comment.Created, comment.CreatedByUserId);

    private static async Task<List<CommentAdminStub>> GetAllPagesAsync(string? search, string? userId)
    {
        var comments = new List<CommentAdminStub>();
        PagedResultSet<CommentAdminStub> page;
        var pageNumber = 1;
        do
        {
            page = await Server.Instance.Comments.GetCommentsAsync(search, userId, pageNumber++, 100);
            comments.AddRange(page.Results);
        } while (page.CurrentPage < page.TotalPages);

        return comments;
    }

    [CosmosFact]
    public async Task GetCommentsAsync_PagesThroughEveryCommentNewestFirst()
    {
        var expected = await GetExpectedCommentsAsync();
        Assert.NotEmpty(expected);

        var actual = await GetAllPagesAsync(null, null);

        Assert.Equal(expected.Select(e => Key(e.GalleryOrImageId, e.Comment.Created, e.Comment.CreatedByUserId)).Order(), actual.Select(Key).Order());
        for (var i = 1; i < actual.Count; i++)
            Assert.True(actual[i - 1].Created >= actual[i].Created, $"comment {i} is out of order");

        // every comment can be linked back to the gallery it's in
        Assert.All(actual, c => Assert.False(string.IsNullOrEmpty(c.GalleryName), $"comment {Key(c)} has no gallery name"));
    }

    [CosmosFact]
    public async Task GetCommentsAsync_SearchMatchesTextOrAuthorName()
    {
        var expected = await GetExpectedCommentsAsync();
        var users = (await CosmosTestDatabase.GetUsersAsync()).ToDictionary(u => u.Id);

        // pick a word from a real comment so the search has something to find
        var search = expected.Select(e => e.Comment.Text).Where(t => t != null).SelectMany(t => t!.Split(' ')).First(w => w.Length > 5);
        var matching = expected
            .Where(e => e.Comment.Text?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                        users.GetValueOrDefault(e.Comment.CreatedByUserId)?.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .Select(e => Key(e.GalleryOrImageId, e.Comment.Created, e.Comment.CreatedByUserId))
            .Order();

        var actual = await GetAllPagesAsync(search.ToUpper(), null);

        Assert.NotEmpty(actual);
        Assert.Equal(matching, actual.Select(Key).Order());
    }

    [CosmosFact]
    public async Task GetCommentsAsync_FiltersByUser()
    {
        var expected = await GetExpectedCommentsAsync();
        var userId = expected.GroupBy(e => e.Comment.CreatedByUserId).MaxBy(g => g.Count())!.Key;

        var actual = await GetAllPagesAsync(null, userId);

        Assert.Equal(expected.Count(e => e.Comment.CreatedByUserId == userId), actual.Count);
        Assert.All(actual, c => Assert.Equal(userId, c.CreatedByUserId));
    }
}
