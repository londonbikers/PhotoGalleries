using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Application.Servers
{
    public class GalleryServer
    {
        #region constructors
        internal GalleryServer()
        {
        }
        #endregion

        #region public methods
        public async Task<Gallery> GetGalleryAsync(string categoryId, string galleryId)
        {
            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var response = await container.ReadItemAsync<Gallery>(galleryId, new PartitionKey(categoryId));
            Debug.WriteLine($"GalleryServer:GetGalleryAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime()} ms");
            return response.Resource;
        }

        public async Task<Gallery> GetGalleryByLegacyNumIdAsync(int galleryLegacyId)
        {
            const string query = "SELECT * FROM c WHERE c.LegacyNumId = @galleryLegacyId";
            var queryDefinition = new QueryDefinition(query);
            queryDefinition.WithParameter("@galleryLegacyId", galleryLegacyId);
            return await GetGalleryByQueryAsync(queryDefinition);
        }

        public async Task<Gallery> GetGalleryByLegacyGuidIdAsync(Guid galleryLegacyId)
        {
            const string query = "SELECT * FROM c WHERE c.LegacyGuidId = @galleryLegacyId";
            var queryDefinition = new QueryDefinition(query);
            queryDefinition.WithParameter("@galleryLegacyId", galleryLegacyId);
            return await GetGalleryByQueryAsync(queryDefinition);
        }

        /// <summary>
        /// Returns a page of galleries residing in a specific category
        /// </summary>
        /// <param name="category">The category the galleries reside in</param>
        /// <param name="page">The page of galleries to return results from, for the first page use 1.</param>
        /// <param name="pageSize">The maximum number of galleries to return per page, i.e. 20.</param>
        /// <param name="maxResults">The maximum number of galleries to get paged results for, i.e. how many pages to look for.</param>
        /// <param name="includeInactiveGalleries">Indicates whether or not inactive (not active) galleries should be returned. False by default.</param>
        public async Task<PagedResultSet<Gallery>> GetGalleriesAsync(Category category, int page = 1, int pageSize = 20, int maxResults = 500, bool includeInactiveGalleries = false)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be a positive number");

            if (page < 1)
                page = 1;

            // limit page size to avoid incurring unnecessary charges and increasing latency
            if (pageSize > 100)
                pageSize = 100;

            // limit how big the id query is to avoid unnecessary charges and to keep latency within an acceptable range
            if (maxResults > 500)
                maxResults = 500;

            // get the complete list of ids
            var queryDefinition = includeInactiveGalleries
                ? new QueryDefinition("SELECT TOP @maxResults VALUE g.id FROM g WHERE g.CategoryId = @categoryId ORDER BY g.Created DESC").WithParameter("@maxResults", maxResults).WithParameter("@categoryId", category.Id)
                : new QueryDefinition("SELECT TOP @maxResults VALUE g.id FROM g WHERE g.CategoryId = @categoryId AND g.Active = true ORDER BY g.Created DESC").WithParameter("@maxResults", maxResults).WithParameter("@categoryId", category.Id);
            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var queryResult = container.GetItemQueryIterator<object>(queryDefinition);
            var ids = new List<DatabaseId>();
            double charge = 0;
            TimeSpan elapsedTime = default;

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                ids.AddRange(results.Select(result => new DatabaseId(result.ToString(), category.Id)));
                charge += results.RequestCharge;
                elapsedTime += results.Diagnostics.GetClientElapsedTime();
            }

            Debug.WriteLine($"GalleryServer.GetGalleriesAsync(Category): Found {ids.Count} ids using query: {queryDefinition.QueryText}");
            Debug.WriteLine($"GalleryServer.GetGalleriesAsync(Category): Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            // now with all the ids we know how many total results there are and so can populate paging info
            var pagedResultSet = new PagedResultSet<Gallery> { PageSize = pageSize, TotalResults = ids.Count, CurrentPage = page };

            // don't let users try and request a page that doesn't exist
            if (page > pagedResultSet.TotalPages)
                page = pagedResultSet.TotalPages;

            // now just retrieve a page's worth of galleries from the results
            var offset = (page - 1) * pageSize;
            var itemsToGet = ids.Count >= pageSize ? pageSize : ids.Count;

            // if we're on the last page just get the remaining items
            if (page == pagedResultSet.TotalPages)
                itemsToGet = pagedResultSet.TotalResults - offset;

            if (ids.Count == 0)
                return null;

            var pageIds = ids.GetRange(offset, itemsToGet);
            foreach (var id in pageIds)
                pagedResultSet.Results.Add(await GetGalleryAsync(id.PartitionKey, id.Id));

            return pagedResultSet;
        }

        public async Task<List<GalleryAdminStub>> GetLatestGalleriesAsync(int maxResults)
        {
            // limit the results to avoid putting excessive strain on the database and from incurring unnecessary charges
            if (maxResults > 100)
                maxResults = 100;

            var queryDefinition = new QueryDefinition("SELECT TOP @pageSize c.id, c.CategoryId, c.Name, c.Active, c.Created FROM c ORDER BY c.Created DESC").WithParameter("@pageSize", maxResults);
            return await GetGalleryStubsByQueryAsync(queryDefinition);
        }

        public async Task<List<Gallery>> GetLatestActiveGalleriesAsync(int maxResults)
        {
            // limit the results to avoid putting excessive strain on the database and from incurring unnecessary charges
            if (maxResults > 100)
                maxResults = 100;

            var queryDefinition = new QueryDefinition("SELECT TOP @pageSize c.id AS Id, c.CategoryId AS PartitionKey FROM c WHERE c.Active = true ORDER BY c.Created DESC").WithParameter("@pageSize", maxResults);
            var databaseIds = await Server.GetIdsByQueryAsync(Constants.GalleriesContainerName, queryDefinition);
            var galleries = new List<Gallery>();

            foreach (var databaseId in databaseIds)
                galleries.Add(await GetGalleryAsync(databaseId.PartitionKey, databaseId.Id));

            return galleries;
        }

        /// <summary>
        /// Returns a page of galleries that match a search term.
        /// </summary>
        /// <param name="term">The search term to use to search for galleries.</param>
        /// <param name="page">The page of galleries to return results from, for the first page use 1.</param>
        /// <param name="pageSize">The maximum number of galleries to return per page, i.e. 20.</param>
        /// <param name="maxResults">The maximum number of galleries to get paged results for, i.e. how many pages to look for.</param>
        /// <param name="includeInactiveGalleries">Indicates whether or not inactive (not active) galleries should be returned. False by default.</param>
        public async Task<PagedResultSet<Gallery>> SearchForGalleriesAsync(string term, int page = 1, int pageSize = 20, int maxResults = 500, bool includeInactiveGalleries = false)
        {
            if (string.IsNullOrEmpty(term))
                throw new ArgumentNullException(nameof(term));

            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be a positive number");

            if (page < 1)
                page = 1;

            // limit page size to avoid incurring unnecessary charges and increasing latency
            if (pageSize > 100)
                pageSize = 100;

            // limit how big the id query is to avoid unnecessary charges and to keep latency within an acceptable range
            if (maxResults > 500)
                maxResults = 500;

            // get the complete list of ids
            var queryText = includeInactiveGalleries
                ? "SELECT TOP @maxResults g.id AS Id, g.CategoryId AS PartitionKey FROM g WHERE CONTAINS(g.Name, @term, true) ORDER BY g.Created DESC"
                : "SELECT TOP @maxResults g.id AS Id, g.CategoryId AS PartitionKey FROM g WHERE CONTAINS(g.Name, @term, true) AND g.Active = true ORDER BY g.Created DESC";

            var queryDefinition = new QueryDefinition(queryText)
                .WithParameter("@maxResults", maxResults)
                .WithParameter("@term", term);

            var databaseIds = await Server.GetIdsByQueryAsync(Constants.GalleriesContainerName, queryDefinition);

            // now with all the ids we know how many total results there are and so can populate paging info
            var pagedResultSet = new PagedResultSet<Gallery> { PageSize = pageSize, TotalResults = databaseIds.Count, CurrentPage = page };

            // don't let users try and request a page that doesn't exist
            if (page > pagedResultSet.TotalPages)
                return null;

            // now just retrieve a page's worth of galleries from the results
            var offset = (page - 1) * pageSize;
            var itemsToGet = databaseIds.Count >= pageSize ? pageSize : databaseIds.Count;

            // if we're on the last page just get the remaining items
            if (page == pagedResultSet.TotalPages)
                itemsToGet = pagedResultSet.TotalResults - offset;

            if (databaseIds.Count == 0)
                return pagedResultSet;

            var pageIds = databaseIds.GetRange(offset, itemsToGet);
                
            // this is how we used to do it, and it used to be 3-5x slower
            //foreach (var id in pageIds)
            //    pagedResultSet.Results.Add(await GetGalleryAsync(id.PartitionKey, id.Id));

            var unorderedGalleries = new List<Gallery>();
            var tasks = new List<Task>();
            foreach (var pageId in pageIds)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var g = await GetGalleryAsync(pageId.PartitionKey, pageId.Id);
                    lock (unorderedGalleries)
                        unorderedGalleries.Add(g);
                }));
            }

            var t = Task.WhenAll(tasks);
            t.Wait();

            // put the unordered galleries into the results list in the same order as the ids were retrieved from the db
            foreach (var id in pageIds)
                pagedResultSet.Results.Add(unorderedGalleries.SingleOrDefault(g => g.Id == id.Id));

            return pagedResultSet;
        }

        public async Task UpdateGalleryAsync(Gallery gallery)
        {
            if (gallery == null)
                throw new ArgumentNullException(nameof(gallery));

            if (!gallery.IsValid())
                throw new InvalidOperationException("Gallery is not valid. Check that all required properties are set");

            // this is a good opportunity to verify that we have the right image count.
            // we do this as we rely on the client to update image counts after a batch of uploads is complete,
            // but of course we can't trust that the client will always do this. the user might close the browser mid-upload for example.
            gallery.ImageCount = await GetGalleryImageCount(gallery);

            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var partitionKey = new PartitionKey(gallery.CategoryId);
            var response = await container.ReplaceItemAsync(gallery, gallery.Id, partitionKey);
            Debug.WriteLine($"GalleryServer.CreateOrUpdateGalleryAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
        }

        public async Task CreateGalleryAsync(Gallery gallery)
        {
            if (gallery == null)
                throw new ArgumentNullException(nameof(gallery));

            if (!gallery.IsValid())
                throw new InvalidOperationException("Gallery is not valid. Check that all required properties are set");

            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var partitionKey = new PartitionKey(gallery.CategoryId);
            var response = await container.CreateItemAsync(gallery, partitionKey);
            Debug.WriteLine($"GalleryServer.CreateOrUpdateGalleryAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
        }

        /// <summary>
        /// Permanently deletes the gallery and all images including all image files in storage.
        /// </summary>
        public async Task DeleteGalleryAsync(string categoryId, string galleryId)
        {
            var gallery = await GetGalleryAsync(categoryId, galleryId);

            // delete all images docs and files. can't be done in bulk unfortunately.
            foreach (var image in await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id))
                await Server.Instance.Images.DeleteImageAsync(image, true);

            // delete the gallery doc
            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var response = await container.DeleteItemAsync<Gallery>(gallery.Id, new PartitionKey(gallery.CategoryId));
            Debug.WriteLine($"GalleryServer.DeleteGalleryAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
        }

        public async Task ChangeGalleryCategoryAsync(string currentCategoryId, string galleryId, string newCategoryId)
        {
            // delete old gallery database object
            // create new gallery database object
            // update image category database object references

            var g = await GetGalleryAsync(currentCategoryId, galleryId);
            g.CategoryId = newCategoryId;

            var galleryContainer = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            await galleryContainer.DeleteItemAsync<Gallery>(g.Id, new PartitionKey(currentCategoryId));

            // should we pause here to wait for replication to take effect?
            await galleryContainer.CreateItemAsync(g, new PartitionKey(g.CategoryId));

            var images = await Server.Instance.Images.GetGalleryImagesAsync(g.Id);
            foreach (var i in images)
            {
                i.GalleryCategoryId = g.CategoryId;
                await Server.Instance.Images.UpdateImageAsync(i);
            }
        }

        /// <summary>
        /// Updates the count we keep on a gallery for the number of images it contains.
        /// This avoids having to collect the big image objects from the database every time we just want a simple count of the number of images in the gallery.
        /// </summary>
        public async Task UpdateGalleryImageCount(string categoryId, string galleryId)
        {
            var gallery = await GetGalleryAsync(categoryId, galleryId);
            gallery.ImageCount = await GetGalleryImageCount(gallery);
            await UpdateGalleryAsync(gallery);
        }

        public async Task CreateCommentAsync(string comment, string userId, bool receiveNotifications, string categoryId, string galleryId)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            var galleryComment = new Comment
            {
                CreatedByUserId = userId,
                Text = comment.Trim()
            };

            gallery.Comments.Add(galleryComment);

            // subscribe the user to comment notifications if they've asked to be
            if (receiveNotifications)
            {
                // create a comment subscription
                if (!gallery.UserCommentSubscriptions.Contains(userId))
                    gallery.UserCommentSubscriptions.Add(userId);

                // todo: later on limit how many notifications a user gets for a single object
                // add message to queue for notifications

                // notification sender needs to know:
                // - object id 1 (i.e. category id)
                // - object id 2 (i.e. gallery id
                // - object type
                // - when they commented
                // i.e. 12,13,image,01/01/2021 18:54:00

                // create the message and send to the Azure Storage notifications queue
                var message = $"{categoryId}:{galleryId}:gallery:{galleryComment.Created.Ticks}";
                var encodedMessage = Utilities.Base64Encode(message);
                await Server.Instance.NotificationProcessingQueueClient.SendMessageAsync(encodedMessage);
            }

            await UpdateGalleryAsync(gallery);
        }

        /// <summary>
        /// Updates all of the image position values by re-ordering the images in a gallery by a given property.
        /// </summary>
        public async Task OrderImagesAsync(Gallery gallery, OrderBy orderBy)
        {
            var images = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            List<Image> orderedImages = null;

            switch (orderBy)
            {
                case OrderBy.Name:
                    orderedImages = images.OrderBy(q => q.Name).ToList();
                    break;
                case OrderBy.Filename:
                    orderedImages = images.OrderBy(q => q.Metadata.OriginalFilename).ToList();
                    break;
                case OrderBy.TakenDate:
                    orderedImages = images.OrderBy(q => q.Metadata.TakenDate).ToList();
                    break;
            }

            if (orderedImages != null)
            {
                for (var x = 0; x < orderedImages.Count; x++)
                {
                    var image = orderedImages[x];
                    image.Position = x;
                    await Server.Instance.Images.UpdateImageAsync(image);
                    Debug.WriteLine($"OrderImagesAsync(): Updated image {image.Id} with position {image.Position}");
                }
            }
            else
            {
                throw new InvalidOperationException("Ordered images were not produced");
            }
        }
        #endregion

        #region admin methods
        public async Task<int> GetMissingThumbnailGalleriesCountAsync()
        {
            var query = new QueryDefinition("SELECT VALUE COUNT(0) FROM g WHERE IS_NULL(g.ThumbnailFiles)");
            return await GetGalleriesScalarByQueryAsync(query);
        }

        public async Task AssignMissingThumbnailsAsync()
        {
            // get ids of galleries with missing thumbs
            // try and assign thumbnail for each one
            var query = new QueryDefinition("SELECT * FROM g WHERE IS_NULL(g.ThumbnailFiles)");
            var galleries = await GetGalleriesByQueryAsync(query);
            foreach (var gallery in galleries)
                await AssignMissingThumbnailAsync(gallery);
        }
        #endregion

        #region internal methods
        internal async Task<int> GetGalleriesScalarByQueryAsync(QueryDefinition queryDefinition)
        {
            if (queryDefinition == null)
                throw new InvalidOperationException("queryDefinition is null");

            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var result = container.GetItemQueryIterator<object>(queryDefinition);

            if (!result.HasMoreResults)
                return 0;

            var resultSet = await result.ReadNextAsync();
            Debug.WriteLine("GalleryServer.GetGalleriesScalarByQueryAsync: Query: " + queryDefinition.QueryText);
            Debug.WriteLine($"GalleryServer.GetGalleriesScalarByQueryAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

            return Convert.ToInt32(resultSet.Resource.First());
        }

        internal async Task<List<GalleryAdminStub>> GetGalleryStubsByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var queryResult = container.GetItemQueryIterator<GalleryAdminStub>(queryDefinition);
            var galleryStubs = new List<GalleryAdminStub>();
            double charge = 0;
            TimeSpan elapsedTime = default;

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                galleryStubs.AddRange(results);
                charge += results.RequestCharge;
                elapsedTime += results.Diagnostics.GetClientElapsedTime();
            }

            Debug.WriteLine($"GalleryServer.GetGalleryStubsByQueryAsync: Found {galleryStubs.Count} gallery stubs using query: {queryDefinition.QueryText}");
            Debug.WriteLine($"GalleryServer.GetGalleryStubsByQueryAsync: Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            return galleryStubs;
        }

        internal async Task<List<Gallery>> GetGalleriesByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var queryResult = container.GetItemQueryIterator<Gallery>(queryDefinition);
            var galleries = new List<Gallery>();
            double charge = 0;
            TimeSpan elapsedTime = default;

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                galleries.AddRange(results);
                charge += results.RequestCharge;
                elapsedTime += results.Diagnostics.GetClientElapsedTime();
            }

            Debug.WriteLine($"GalleryServer.GetGalleriesByQueryAsync: Found {galleries.Count} galleries using query: {queryDefinition.QueryText}");
            Debug.WriteLine($"GalleryServer.GetGalleriesByQueryAsync: Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            return galleries;
        }
        #endregion

        #region private methods
        public async Task<Gallery> GetGalleryByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var queryResult = container.GetItemQueryIterator<Gallery>(queryDefinition);

            if (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                foreach (var gallery in resultSet)
                {
                    Debug.WriteLine("GalleryServer.GetGalleryAsync: Found a gallery using query: " + queryDefinition.QueryText);
                    Debug.WriteLine($"$GalleryServer.GetGalleryAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
                    return gallery;
                }
            }

            throw new InvalidOperationException("No gallery found using query: " + queryDefinition.QueryText);
        }

        private static async Task<int> GetGalleryImageCount(Gallery gallery)
        {
            var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM i WHERE i.GalleryId = @galleryId").WithParameter("@galleryId", gallery.Id);
            var count = await Server.Instance.Images.GetImagesScalarByQueryAsync(query);
            return count;
        }

        private async Task AssignMissingThumbnailAsync(Gallery gallery)
        {
            // get the first image
            // try and get where position = 0 first
            // if no results, then get where date created is earliest

            var query = "SELECT TOP 1 * FROM i WHERE i.GalleryId = @galleryId AND i.Position = 0";
            var queryDefinition = new QueryDefinition(query).WithParameter("@galleryId", gallery.Id);
            var imagesContainer = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = imagesContainer.GetItemQueryIterator<Image>(queryDefinition);
            ImageFiles imageFiles = null;

            while (queryResult.HasMoreResults)
            {
                var queryResponse = await queryResult.ReadNextAsync();
                Debug.WriteLine($"GalleryServer.AssignMissingThumbnailAsync() - Position query charge: {queryResponse.RequestCharge}. GalleryId: {gallery.Id}");

                foreach (var item in queryResponse.Resource)
                {
                    imageFiles = item.Files;
                    Debug.WriteLine($"GalleryServer.AssignMissingThumbnailAsync() - Got thumbnail image via Position query. GalleryId: {gallery.Id}");
                    break;
                }
            }

            if (imageFiles == null)
            {
                // no position value set on images, get first image created
                query = "SELECT TOP 1 * FROM i WHERE i.GalleryId = @galleryId ORDER BY i.Created";
                queryDefinition = new QueryDefinition(query).WithParameter("@galleryId", gallery.Id);
                queryResult = imagesContainer.GetItemQueryIterator<Image>(queryDefinition);

                while (queryResult.HasMoreResults)
                {
                    var queryResponse = await queryResult.ReadNextAsync();
                    Debug.WriteLine($"GalleryServer.AssignMissingThumbnailAsync() - Date query charge: {queryResponse.RequestCharge}. GalleryId: {gallery.Id}");

                    foreach (var item in queryResponse.Resource)
                    {
                        imageFiles = item.Files;
                        Debug.WriteLine($"GalleryServer.AssignMissingThumbnailAsync() - Got thumbnail image via date query. GalleryId: {gallery.Id}");
                        break;
                    }
                }
            }

            if (imageFiles == null)
            {
                Debug.WriteLine($"GalleryServer.AssignMissingThumbnailAsync() - No thumbnail candidate, yet. GalleryId: {gallery.Id}");
                return;
            }

            gallery.ThumbnailFiles = imageFiles;
            await UpdateGalleryAsync(gallery);
            Debug.WriteLine($"GalleryServer.AssignMissingThumbnailAsync() - Gallery updated. GalleryId: {gallery.Id}");
        }
        #endregion
    }
}
