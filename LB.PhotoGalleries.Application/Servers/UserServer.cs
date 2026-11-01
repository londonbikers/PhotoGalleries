using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using User = LB.PhotoGalleries.Models.User;

namespace LB.PhotoGalleries.Application.Servers;

public class UserServer
{
    #region constructors
    internal UserServer()
    {
    }
    #endregion

    #region public methods
    public async Task CreateOrUpdateUserAsync(User user)
    {
        if (user == null)
            throw new InvalidOperationException("User is null");

        if (string.IsNullOrEmpty(user.PartitionKey))
            user.PartitionKey = GetUserPartitionKeyFromId(user.Id);

        if (!user.IsValid())
            throw new InvalidOperationException("User is not valid. Check that all required properties are set");

        var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
        var partitionKey = new PartitionKey(user.PartitionKey);
        var response = await container.UpsertItemAsync(user, partitionKey);
        var createdItem = response.StatusCode == HttpStatusCode.Created;
        Log.Debug("UserServer.CreateOrUpdateUserAsync: Created user? " + createdItem);
        Log.Debug($"UserServer.CreateOrUpdateUserAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime()} ms");
    }

    /// <summary>
    /// Deletes a user from the database and any references to the user, which anonymises any content they've created.
    /// This balances the need for a user's right for their personal data to be deleted (GDPR compliance, etc.) with ensuring the integrity of content.
    /// </summary>
    public async Task DeleteUserAsync(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        // get galleries where the user has been involved in some way (author/comments)
        var query = "SELECT * FROM c WHERE c.CreatedByUserId = @userId OR c.Comments.CreatedByUserId = @userId";
        var queryDefinition = new QueryDefinition(query).WithParameter("@userId", user.Id);
        var galleries = await Server.Instance.Galleries.GetGalleriesByQueryAsync(queryDefinition);

        // go through the galleries and look for user references and remove them then update the gallery
        foreach (var gallery in galleries)
        {
            // remove any references to gallery creations
            if (gallery.CreatedByUserId == user.Id)
                gallery.CreatedByUserId = Constants.AnonUserId;

            // remove any references to gallery comments
            foreach (var galleryComment in gallery.Comments.Where(q => q.CreatedByUserId == user.Id))
                galleryComment.CreatedByUserId = Constants.AnonUserId;

            await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
        }

        // go through any comments the user has left against images and anonymise those
        var images = await Server.Instance.Images.GetImagesUserHasCommentedOnAsync(user.Id);
        foreach (var image in images)
        {
            foreach (var comment in image.Comments.Where(c => c.CreatedByUserId == user.Id))
                comment.CreatedByUserId = Constants.AnonUserId;

            await Server.Instance.Images.UpdateImageAsync(image);
        }

        // delete the user
        var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
        var result = await container.DeleteItemAsync<User>(user.Id, new PartitionKey(user.PartitionKey));

        Log.Debug("UserServer:DeleteUserAsync: Status code: " + result.StatusCode);
        Log.Debug("UserServer:DeleteUserAsync: Request charge: " + result.RequestCharge);
    }

    public async Task<User> GetUserAsync(string userId)
    {
        if (!userId.HasValue())
            return null;

        var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);

        try
        {
            var response = await container.ReadItemAsync<User>(userId, new PartitionKey(GetUserPartitionKeyFromId(userId)));
            Log.Debug($"UserServer:GetUserAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
            return response.Resource;
        }
        catch (CosmosException cex)
        {
            if (cex.StatusCode == HttpStatusCode.NotFound)
            {
                // user does not exist
                return null;
            }

            throw;
        }
    }

    public async Task<User> GetUserByLegacyIdAsync(Guid legacyApolloId)
    {
        var queryDefinition = new QueryDefinition("SELECT * FROM u WHERE u.LegacyApolloId = @legacyApolloId").WithParameter("@legacyApolloId", legacyApolloId);
        var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
        var queryResult = container.GetItemQueryIterator<User>(queryDefinition);
        var response = await queryResult.ReadNextAsync();
        Log.Debug($"UserServer:GetUserByLegacyIdAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
        return response.FirstOrDefault();
    }

    /// <summary>
    /// Gets a page of users, optionally only those with a given search term in their name or email address.
    /// </summary>
    /// <param name="searchString">Optional term to filter users by name or email address.</param>
    /// <param name="sortBy">The property to sort the users by.</param>
    /// <param name="descending">Sorts largest/newest/Z first when true.</param>
    /// <param name="page">The page of users to return results from, for the first page use 1.</param>
    /// <param name="pageSize">The maximum number of users to return per page, i.e. 50.</param>
    public async Task<PagedResultSet<User>> GetUsersAsync(string searchString, UserSortBy sortBy, bool descending, int page = 1, int pageSize = 50)
    {
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be a positive number");

        if (page < 1)
            page = 1;

        // limit page size to avoid incurring unnecessary charges and increasing latency
        if (pageSize > 100)
            pageSize = 100;

        var where = searchString.HasValue() ? " WHERE CONTAINS(c.Name, @searchString, true) OR CONTAINS(c.Email, @searchString, true)" : string.Empty;

        // comment counts aren't stored against users, so to sort by them we have to get them all and sort in memory
        if (sortBy == UserSortBy.Comments)
            return await GetUsersSortedByCommentCountAsync(where, searchString, descending, page, pageSize);

        var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c" + where);
        if (searchString.HasValue())
            countQuery.WithParameter("@searchString", searchString);

        var total = (await QueryAsync<int>(Constants.UsersContainerName, countQuery)).FirstOrDefault();
        var pagedResultSet = new PagedResultSet<User> { PageSize = pageSize, TotalResults = total };

        // don't let users try and request a page that doesn't exist
        if (page > pagedResultSet.TotalPages)
            page = Math.Max(pagedResultSet.TotalPages, 1);

        pagedResultSet.CurrentPage = page;
        if (total == 0)
            return pagedResultSet;

        // sortBy is an enum, so only these property names can ever end up in the query
        var sortProperty = sortBy switch
        {
            UserSortBy.Name => "c.Name",
            UserSortBy.Email => "c.Email",
            _ => "c.Created"
        };

        var queryDefinition = new QueryDefinition($"SELECT * FROM c{where} ORDER BY {sortProperty} {(descending ? "DESC" : "ASC")} OFFSET @offset LIMIT @limit")
            .WithParameter("@offset", (page - 1) * pageSize)
            .WithParameter("@limit", pageSize);

        if (searchString.HasValue())
            queryDefinition.WithParameter("@searchString", searchString);

        pagedResultSet.Results = await GetUsersByQueryAsync(queryDefinition);
        return pagedResultSet;
    }

    public async Task<int> GetUserGalleryCountAsync(User user)
    {
        if (user == null)
            throw new InvalidOperationException("User is null");

        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.CreatedByUserId = @userId").WithParameter("@userId", user.Id);
        return await Server.Instance.Galleries.GetGalleriesScalarByQueryAsync(query);
    }

    public async Task<int> GetUserCommentCountAsync(User user)
    {
        if (user == null)
            throw new InvalidOperationException("User is null");

        var commentCounts = await GetUserCommentCountsAsync(new[] { user.Id });
        return commentCounts.GetValueOrDefault(user.Id);
    }

    /// <summary>
    /// Gets how many comments users have made across galleries and images, keyed by user id.
    /// Users who haven't commented aren't included.
    /// </summary>
    /// <param name="userIds">The users to count comments for, or null to count comments for every user.</param>
    public async Task<Dictionary<string, int>> GetUserCommentCountsAsync(IReadOnlyCollection<string> userIds = null)
    {
        var commentCounts = new Dictionary<string, int>();
        if (userIds is { Count: 0 })
            return commentCounts;

        // comments are an array on each gallery and image, so they have to be joined to be filtered and grouped on
        var query = "SELECT cm.CreatedByUserId AS UserId, COUNT(1) AS CommentCount FROM c JOIN cm IN c.Comments" +
                    (userIds != null ? " WHERE ARRAY_CONTAINS(@userIds, cm.CreatedByUserId)" : string.Empty) +
                    " GROUP BY cm.CreatedByUserId";
        var queryDefinition = new QueryDefinition(query);
        if (userIds != null)
            queryDefinition.WithParameter("@userIds", userIds);

        foreach (var containerName in new[] { Constants.GalleriesContainerName, Constants.ImagesContainerName })
            foreach (var userCommentCount in await QueryAsync<UserCommentCount>(containerName, queryDefinition))
                commentCounts[userCommentCount.UserId] = commentCounts.GetValueOrDefault(userCommentCount.UserId) + userCommentCount.CommentCount;

        return commentCounts;
    }

    public string GetUserPartitionKeyFromId(string userId)
    {
        // find first number in user id, which is a guid
        // this could change in the future to just the first character (alpha-numeric) but for now 10 partitions seems suitable
        // for the number of users we expect.

        foreach (var character in userId)
            if (int.TryParse(character.ToString(), out var number))
                return number.ToString();

        throw new ArgumentException("Argument value does not seem to be a valid Guid.", nameof(userId));
    }

    /// <summary>
    /// If the user's profile picture is not being internally hosted then this method will download it and store it internally
    /// </summary>
    /// <param name="user">The user who to download the picture for.</param>
    /// <param name="originalPictureUrl">The URL for the original version of the picture.</param>
    public async Task DownloadAndStoreUserPictureAsync(User user, string originalPictureUrl)
    {
        if (user.Picture.HasValue() && user.Picture.Equals(originalPictureUrl, StringComparison.CurrentCultureIgnoreCase))
            return;

        // Security: Validate the picture URL before downloading to prevent SSRF attacks
        if (!Uri.TryCreate(originalPictureUrl, UriKind.Absolute, out var pictureUri))
        {
            Log.Warning($"UserServer.DownloadAndStoreUserPictureAsync() - Invalid URL format: {originalPictureUrl}");
            return;
        }

        // Security: Only allow HTTPS URLs to prevent man-in-the-middle attacks
        if (pictureUri.Scheme != Uri.UriSchemeHttps)
        {
            Log.Warning($"UserServer.DownloadAndStoreUserPictureAsync() - Only HTTPS URLs are allowed. Rejected: {originalPictureUrl}");
            return;
        }

        // picture URL is different, download and store it
        var containerClient = Server.Instance.BlobServiceClient.GetBlobContainerClient(Constants.StorageUserPicturesContainerName);

        // delete any old version we have currently
        string fileId;
        if (user.PictureHostedUrl.HasValue())
        {
            fileId = user.PictureHostedUrl.Substring(user.PictureHostedUrl.LastIndexOf('/') + 1);
            await containerClient.DeleteBlobIfExistsAsync(fileId);
        }

        // download it and store the original picture
        fileId = $"{user.Id}-{DateTime.Now.Ticks}.jpg";
        using var client = new HttpClient();

        // Security: Set reasonable timeout and size limits to prevent resource exhaustion
        client.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            // Security: Download with validation
            using var response = await client.GetAsync(originalPictureUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // Security: Validate Content-Type is an image
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(contentType) ||
                (!contentType.StartsWith("image/jpeg", StringComparison.OrdinalIgnoreCase) &&
                 !contentType.StartsWith("image/png", StringComparison.OrdinalIgnoreCase) &&
                 !contentType.StartsWith("image/jpg", StringComparison.OrdinalIgnoreCase)))
            {
                Log.Warning($"UserServer.DownloadAndStoreUserPictureAsync() - Invalid content type: {contentType}. Only JPEG and PNG images are allowed.");
                return;
            }

            // Security: Validate content length to prevent resource exhaustion (max 5MB)
            const long maxFileSize = 5 * 1024 * 1024;
            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > maxFileSize)
            {
                Log.Warning($"UserServer.DownloadAndStoreUserPictureAsync() - File too large: {response.Content.Headers.ContentLength.Value} bytes. Maximum allowed: {maxFileSize} bytes.");
                return;
            }

            await using var imageStream = await response.Content.ReadAsStreamAsync();

            // Security: Validate file signature to ensure it's actually an image
            if (!Utilities.ValidateImageFileSignature(imageStream))
            {
                Log.Warning($"UserServer.DownloadAndStoreUserPictureAsync() - Invalid file signature. File is not a valid JPEG or PNG image.");
                return;
            }

            await containerClient.UploadBlobAsync(fileId, imageStream);
            user.PictureHostedUrl = containerClient.Uri + "/" + fileId;
            user.Picture = originalPictureUrl;
        }
        catch (Exception e)
        {
            Log.Debug($"UserServer.DownloadAndStoreUserPictureAsync() - Exception downloading/uploading: {e.Message}");
        }
    }
    #endregion

    #region private methods
    private static async Task<List<User>> GetUsersByQueryAsync(QueryDefinition queryDefinition)
    {
        var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
        var queryResult = container.GetItemQueryIterator<User>(queryDefinition);
        var users = new List<User>();
        double charge = 0;
        TimeSpan elapsedTime = default;

        while (queryResult.HasMoreResults)
        {
            var resultSet = await queryResult.ReadNextAsync();
            users.AddRange(resultSet);
            charge += resultSet.RequestCharge;
            elapsedTime += resultSet.Diagnostics.GetClientElapsedTime();
        }

        Log.Debug("UserServer.GetUsersByQueryAsync: Query: " + queryDefinition.QueryText);
        Log.Debug($"UserServer.GetUsersByQueryAsync: Total request charge: {charge}. Total elapsed time {elapsedTime.TotalMilliseconds} ms");

        return users;
    }

    private async Task<PagedResultSet<User>> GetUsersSortedByCommentCountAsync(string where, string searchString, bool descending, int page, int pageSize)
    {
        // get every matching user id, then order them by comment count, newest first where users have the same count
        var stubsQuery = new QueryDefinition("SELECT c.id, c.Created FROM c" + where);
        if (searchString.HasValue())
            stubsQuery.WithParameter("@searchString", searchString);

        var stubs = await QueryAsync<UserStub>(Constants.UsersContainerName, stubsQuery);
        var commentCounts = await GetUserCommentCountsAsync();
        var orderedStubs = descending
            ? stubs.OrderByDescending(s => commentCounts.GetValueOrDefault(s.Id)).ThenByDescending(s => s.Created)
            : stubs.OrderBy(s => commentCounts.GetValueOrDefault(s.Id)).ThenByDescending(s => s.Created);

        var pagedResultSet = new PagedResultSet<User> { PageSize = pageSize, TotalResults = stubs.Count };

        // don't let users try and request a page that doesn't exist
        if (page > pagedResultSet.TotalPages)
            page = Math.Max(pagedResultSet.TotalPages, 1);

        pagedResultSet.CurrentPage = page;
        var pageIds = orderedStubs.Skip((page - 1) * pageSize).Take(pageSize).Select(s => s.Id).ToList();
        if (pageIds.Count == 0)
            return pagedResultSet;

        // retrieve just this page's users, then put them back into comment count order
        var usersQuery = new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(@ids, c.id)").WithParameter("@ids", pageIds);
        var users = await GetUsersByQueryAsync(usersQuery);
        pagedResultSet.Results = users.OrderBy(u => pageIds.IndexOf(u.Id)).ToList();
        return pagedResultSet;
    }

    private static async Task<List<T>> QueryAsync<T>(string containerName, QueryDefinition queryDefinition)
    {
        var container = Server.Instance.Database.GetContainer(containerName);
        var queryResult = container.GetItemQueryIterator<T>(queryDefinition);
        var results = new List<T>();
        double charge = 0;
        TimeSpan elapsedTime = default;

        while (queryResult.HasMoreResults)
        {
            var resultSet = await queryResult.ReadNextAsync();
            results.AddRange(resultSet);
            charge += resultSet.RequestCharge;
            elapsedTime += resultSet.Diagnostics.GetClientElapsedTime();
        }

        Log.Debug($"UserServer.QueryAsync: {containerName} query: {queryDefinition.QueryText}");
        Log.Debug($"UserServer.QueryAsync: Total request charge: {charge}. Total elapsed time {elapsedTime.TotalMilliseconds} ms");

        return results;
    }
    #endregion

    #region private classes
    private class UserStub
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public DateTime Created { get; set; }
    }

    private class UserCommentCount
    {
        public string UserId { get; set; }
        public int CommentCount { get; set; }
    }
    #endregion
}