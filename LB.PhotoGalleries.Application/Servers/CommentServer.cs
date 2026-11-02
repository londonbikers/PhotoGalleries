using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Application.Servers;

/// <summary>
/// Works with comments across galleries and images together. Comments are stored as arrays on galleries and images,
/// so creating and deleting them for a single gallery or image is done by GalleryServer and ImageServer.
/// </summary>
public class CommentServer
{
    #region constructors
    internal CommentServer()
    {
    }
    #endregion

    #region public methods
    /// <summary>
    /// Gets a page of comments made on galleries and images, newest first.
    /// </summary>
    /// <param name="searchString">Optional term to filter comments by, matched against the comment text and the name of the user who made it.</param>
    /// <param name="userId">Optional id of a user to only return the comments of.</param>
    /// <param name="page">The page of comments to return results from, for the first page use 1.</param>
    /// <param name="pageSize">The maximum number of comments to return per page, i.e. 50.</param>
    public async Task<PagedResultSet<CommentAdminStub>> GetCommentsAsync(string searchString, string userId, int page = 1, int pageSize = 50)
    {
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be a positive number");

        if (page < 1)
            page = 1;

        // limit page size to avoid incurring unnecessary charges and increasing latency
        if (pageSize > 100)
            pageSize = 100;

        // comments are an array on each gallery and image, so they have to be joined to be filtered on
        var conditions = new List<string>();
        if (userId.HasValue())
            conditions.Add("cm.CreatedByUserId = @userId");

        List<string> authorIds = null;
        if (searchString.HasValue())
        {
            var authorsQuery = new QueryDefinition("SELECT VALUE c.id FROM c WHERE CONTAINS(c.Name, @searchString, true)").WithParameter("@searchString", searchString);
            authorIds = await Server.QueryAsync<string>(Constants.UsersContainerName, authorsQuery);
            conditions.Add("(CONTAINS(cm.Text, @searchString, true) OR ARRAY_CONTAINS(@authorIds, cm.CreatedByUserId))");
        }

        var where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : string.Empty;
        var galleryCommentsQuery = BuildQuery("SELECT c.id AS GalleryId, c.CategoryId AS GalleryCategoryId, c.Name AS GalleryName, cm.Created, cm.CreatedByUserId, cm.Text FROM c JOIN cm IN c.Comments" + where, searchString, userId, authorIds);
        var imageCommentsQuery = BuildQuery("SELECT c.GalleryId, c.GalleryCategoryId, c.id AS ImageId, c.Name AS ImageName, cm.Created, cm.CreatedByUserId, cm.Text FROM c JOIN cm IN c.Comments" + where, searchString, userId, authorIds);

        // galleries and images are in different containers, so merge and order them here
        var galleryComments = Server.QueryAsync<CommentAdminStub>(Constants.GalleriesContainerName, galleryCommentsQuery);
        var imageComments = Server.QueryAsync<CommentAdminStub>(Constants.ImagesContainerName, imageCommentsQuery);
        var comments = (await galleryComments).Concat(await imageComments).OrderByDescending(c => c.Created).ToList();

        var pagedResultSet = new PagedResultSet<CommentAdminStub> { PageSize = pageSize, TotalResults = comments.Count };

        // don't let users try and request a page that doesn't exist
        if (page > pagedResultSet.TotalPages)
            page = Math.Max(pagedResultSet.TotalPages, 1);

        pagedResultSet.CurrentPage = page;
        pagedResultSet.Results = comments.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // images don't store their gallery's name, so look those up for just this page
        var galleryIds = pagedResultSet.Results.Where(c => c.IsImageComment).Select(c => c.GalleryId).Distinct().ToList();
        if (galleryIds.Count > 0)
        {
            var galleryNamesQuery = new QueryDefinition("SELECT c.id, c.Name FROM c WHERE ARRAY_CONTAINS(@ids, c.id)").WithParameter("@ids", galleryIds);
            var galleryNames = (await Server.QueryAsync<GalleryName>(Constants.GalleriesContainerName, galleryNamesQuery)).ToDictionary(g => g.Id, g => g.Name);
            foreach (var comment in pagedResultSet.Results.Where(c => c.IsImageComment))
                comment.GalleryName = galleryNames.GetValueOrDefault(comment.GalleryId);
        }

        return pagedResultSet;
    }

    /// <summary>
    /// Deletes a comment from a gallery, or from an image in the gallery if an image id is supplied.
    /// Comments don't have ids, so they're identified by when and by whom they were created.
    /// </summary>
    /// <returns>False if the comment couldn't be found, i.e. it's already been deleted.</returns>
    public async Task<bool> DeleteCommentAsync(string galleryCategoryId, string galleryId, string imageId, long createdTicks, string createdByUserId)
    {
        var gallery = await Server.Instance.Galleries.GetGalleryAsync(galleryCategoryId, galleryId);
        if (!imageId.HasValue())
        {
            var galleryComment = gallery.Comments.SingleOrDefault(c => c.CreatedByUserId == createdByUserId && c.Created.Ticks == createdTicks);
            if (galleryComment == null)
                return false;

            await Server.Instance.Galleries.DeleteCommentAsync(gallery, galleryComment);
            return true;
        }

        var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
        var imageComment = image?.Comments.SingleOrDefault(c => c.CreatedByUserId == createdByUserId && c.Created.Ticks == createdTicks);
        if (imageComment == null)
            return false;

        await Server.Instance.Images.DeleteCommentAsync(gallery, image, imageComment);
        return true;
    }
    #endregion

    #region private methods
    private static QueryDefinition BuildQuery(string query, string searchString, string userId, List<string> authorIds)
    {
        var queryDefinition = new QueryDefinition(query);
        if (userId.HasValue())
            queryDefinition.WithParameter("@userId", userId);

        if (searchString.HasValue())
            queryDefinition.WithParameter("@searchString", searchString).WithParameter("@authorIds", authorIds);

        return queryDefinition;
    }
    #endregion

    #region private classes
    private class GalleryName
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string Name { get; set; }
    }
    #endregion
}
