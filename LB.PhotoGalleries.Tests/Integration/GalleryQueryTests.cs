using LB.PhotoGalleries.Application;

namespace LB.PhotoGalleries.Tests.Integration;

/// <summary>
/// Finding galleries from links, which may have been made before the gallery moved category.
/// </summary>
public class GalleryQueryTests
{
    [CosmosFact]
    public async Task FindGalleryAsync_FindsTheGalleryWhateverCategoryIsGiven()
    {
        var galleries = await CosmosTestDatabase.QueryAsync<GalleryCategoryRow>("Galleries", "SELECT c.id, c.CategoryId FROM c");
        var gallery = galleries.First();
        var otherCategoryId = galleries.Select(g => g.CategoryId).FirstOrDefault(id => id != gallery.CategoryId) ?? "no-such-category";

        foreach (var categoryId in new[] { gallery.CategoryId, otherCategoryId, null })
        {
            var found = await Server.Instance.Galleries.FindGalleryAsync(categoryId, gallery.Id);
            Assert.NotNull(found);
            Assert.Equal(gallery.Id, found.Id);
            Assert.Equal(gallery.CategoryId, found.CategoryId);
        }
    }

    [CosmosFact]
    public async Task FindGalleryAsync_ReturnsNullForAGalleryThatDoesNotExist()
    {
        var categoryId = (await CosmosTestDatabase.QueryAsync<GalleryCategoryRow>("Galleries", "SELECT TOP 1 c.id, c.CategoryId FROM c")).First().CategoryId;

        Assert.Null(await Server.Instance.Galleries.FindGalleryAsync(categoryId, "no-such-gallery"));
        Assert.Null(await Server.Instance.Galleries.FindGalleryAsync(null, "no-such-gallery"));
    }
}

public class GalleryCategoryRow
{
    [Newtonsoft.Json.JsonProperty("id")] public string Id { get; set; } = "";
    public string CategoryId { get; set; } = "";
}
