using Azure.Storage.Blobs.Models;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Models.Exceptions;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Image = LB.PhotoGalleries.Models.Image;

namespace LB.PhotoGalleries.Application.Servers
{
    public class ImageServer
    {
        #region constructors
        internal ImageServer()
        {
            AcceptedContentTypes = new List<string>
            {
                "image/png",
                "image/jpeg"
            };
        }
        #endregion

        #region accessors
        public List<string> AcceptedContentTypes { get; }
        #endregion

        #region public methods
        /// <summary>
        /// Stores an uploaded file in the storage system and adds a supporting Image object to the database.
        /// </summary>
        /// <param name="galleryCategoryId">The id for the category the gallery resides in, which the image resides in.</param>
        /// <param name="galleryId">The gallery this image is going to be contained within.</param>
        /// <param name="imageStream">The stream for the uploaded image file.</param>
        /// <param name="filename">The original filename provided by the client.</param>
        /// <param name="image">Optionally supply a pre-populated Image object.</param>
        /// <param name="performImageDimensionsCheck">Ordinarily images must be bigger than 800x800 in size but for migration purposes we might want to override this.</param>
        public async Task CreateImageAsync(string galleryCategoryId, string galleryId, Stream imageStream, string filename, Image image = null, bool performImageDimensionsCheck = true)
        {
            try
            {
                if (string.IsNullOrEmpty(galleryId))
                    throw new ArgumentNullException(nameof(galleryId));

                if (imageStream == null)
                    throw new ArgumentNullException(nameof(imageStream));

                if (string.IsNullOrEmpty(filename))
                    throw new ArgumentNullException(nameof(filename));

                CheckImageDimensions(imageStream);

                if (image == null)
                {
                    // create the Image object anew
                    var id = Utilities.GenerateId();
                    image = new Image
                    {
                        Id = id,
                        GalleryCategoryId = galleryCategoryId,
                        GalleryId = galleryId,
                        Files = { OriginalId = id + Path.GetExtension(filename).ToLower() }
                    };
                }
                else
                {
                    // the Image already exists but may not be sufficiently populated...
                    if (!image.Id.HasValue())
                        image.Id = Utilities.GenerateId();
                    if (!image.GalleryCategoryId.HasValue())
                        image.GalleryCategoryId = galleryCategoryId;
                    if (!image.GalleryId.HasValue())
                        image.GalleryId = galleryId;
                    if (!image.Files.OriginalId.HasValue())
                        image.Files.OriginalId = image.Id + Path.GetExtension(filename).ToLower();
                }

                // this should be done in the worker when parsing the metadata.
                // we're just not doing it there as that means some work to sanitise and serialise the filename for insertion into the worker message.
                // lazy, I know. I'll come back to this.
                image.Metadata.OriginalFilename = filename;

                if (!image.Name.HasValue())
                    image.Name = Utilities.TidyImageName(Path.GetFileNameWithoutExtension(filename));
                
                if (!image.IsValid())
                    throw new InvalidOperationException("Image would be invalid. Please check all required properties are set.");

                // upload the original file to storage
                var originalContainerClient = Server.Instance.BlobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
                await originalContainerClient.UploadBlobAsync(image.Files.OriginalId, imageStream);

                // create the database record
                var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
                var response = await container.CreateItemAsync(image, new PartitionKey(image.GalleryId));
                Log.Debug($"ImageServer.CreateImageAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

                // have the pre-gen images created by a background process
                await PostProcessImagesAsync(image);
            }
            finally
            {
                // make sure we release valuable server resources in the event of a problem creating the image
                imageStream?.Close();
            }
        }

        /// <summary>
        /// Replaces the image file for an existing image, allowing for mistake corrections, remastering, etc.
        /// </summary>
        /// <param name="galleryCategoryId">The id for the category the image gallery resides in.</param>
        /// <param name="galleryId">The gallery this image is going to be contained within.</param>
        /// <param name="imageId">The id for the image we're updating the file for.</param>
        /// <param name="imageStream">The stream for the uploaded image file.</param>
        /// <param name="filename">The original filename provided by the client.</param>
        /// <returns></returns>
        public async Task ReplaceImageAsync(string galleryCategoryId, string galleryId, string imageId, Stream imageStream, string filename)
        {
            if (string.IsNullOrEmpty(galleryCategoryId))
                throw new ArgumentNullException(nameof(galleryCategoryId));

            if (string.IsNullOrEmpty(galleryId))
                throw new ArgumentNullException(nameof(galleryId));

            if (string.IsNullOrEmpty(imageId))
                throw new ArgumentNullException(nameof(imageId));

            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

            if (string.IsNullOrEmpty(filename))
                throw new ArgumentNullException(nameof(filename));

            var image = await GetImageAsync(galleryId, imageId);
            if (image == null)
                throw new InvalidOperationException("Sorry, that image doesn't exit.");

            // this should be done in the worker when parsing the metadata.
            // we're just not doing it there as that means some work to sanitise and serialise the filename for insertion into the worker message.
            // lazy, I know. I'll come back to this.
            image.Metadata.OriginalFilename = filename;

            if (!image.IsValid())
                throw new InvalidOperationException("Image would be invalid. Please check all required properties are set.");

            // delete the old files from blob storage first and any references to them in the Image.
            await DeleteImageFilesAsync(image, true);

            // create a new id for the new original image file. it's okay for this to be different to the Image id.
            var newid = Utilities.GenerateId();
            image.Files.OriginalId = newid + Path.GetExtension(filename).ToLower();

            // upload the new original file to storage
            var originalContainerClient = Server.Instance.BlobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
            await originalContainerClient.UploadBlobAsync(image.Files.OriginalId, imageStream);

            // now save the changes to the Image
            await UpdateImageAsync(image);

            // have the pre-gen images created by a background process
            // it will handle updating the thumbnail as well if necessary.
            await PostProcessImagesAsync(image);
        }

        /// <summary>
        /// Updates an Image in the database. Must be provided with a complete and recently queried from the database image to avoid losing other recent updates.
        /// </summary>
        public async Task UpdateImageAsync(Image image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (!image.IsValid())
                throw new InvalidOperationException("Image is invalid. Please check all required properties are set.");

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var response = await container.ReplaceItemAsync(image, image.Id, new PartitionKey(image.GalleryId));
            Log.Debug($"ImageServer.UpdateImageAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
        }

        public async Task<List<Image>> GetGalleryImagesAsync(string galleryId)
        {
            const string query = "SELECT * FROM c WHERE c.GalleryId = @galleryId";
            var queryDefinition = new QueryDefinition(query);
            queryDefinition.WithParameter("@galleryId", galleryId);

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = container.GetItemQueryIterator<Image>(queryDefinition);
            double charge = 0;
            TimeSpan elapsedTime = default;
            var images = new List<Image>();

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                images.AddRange(results);
                elapsedTime += results.Diagnostics.GetClientElapsedTime();
                charge += results.RequestCharge;
            }

            Log.Debug($"ImageServer.GetGalleryImagesAsync: Found {images.Count} gallery images");
            Log.Debug($"ImageServer.GetGalleryImagesAsync: Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            return images;
        }

        public async Task<Image> GetImageAsync(string galleryId, string imageId)
        {
            if (string.IsNullOrEmpty(galleryId) || string.IsNullOrEmpty(imageId))
            {
                Log.Debug("ImageServer:GetImageAsync: some args were null, returning null");
                return null;
            }

            try
            {
                var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
                var response = await container.ReadItemAsync<Image>(imageId, new PartitionKey(galleryId));
                Log.Debug($"ImageServer:GetImageAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
                return response.Resource;
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    Log.Debug($"Image not found in db. GalleryId {galleryId}, ImageId {imageId}");
                    return null;
                }

                // some other unexpected exception
                throw;
            }
        }

        public async Task<Image> GetImageByLegacyIdAsync(long legacyId)
        {
            const string query = "SELECT * FROM c WHERE c.LegacyNumId = @legacyId";
            var queryDefinition = new QueryDefinition(query);
            queryDefinition.WithParameter("@legacyId", legacyId);
            return await GetImageByQueryAsync(queryDefinition);
        }

        /// <summary>
        /// Returns a page of images with a specific tag
        /// </summary>
        /// <param name="tag">The tag used to find images for.</param>
        /// <param name="page">The page of galleries to return results from, for the first page use 1.</param>
        /// <param name="pageSize">The maximum number of galleries to return per page, i.e. 20.</param>
        /// <param name="maxResults">The maximum number of galleries to get paged results for, i.e. how many pages to look for.</param>
        /// <param name="querySortBy">How should we sort the search results?</param>
        /// <param name="queryRange">What time range should the search results cover?</param>
        /// <param name="queryDirection">What direction should the sorted search results be shown in?</param>
        public async Task<PagedResultSet<Image>> GetImagesForTagAsync(
            string tag, 
            int page = 1, 
            int pageSize = 20, 
            int maxResults = 500, 
            QuerySortBy querySortBy = QuerySortBy.DateCreated,
            QueryRange queryRange = QueryRange.Forever,
            QueryDirection queryDirection = QueryDirection.Descending)
        {
            if (string.IsNullOrEmpty(tag))
                throw new ArgumentNullException(nameof(tag));

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

            //var direction = queryDirection == QueryDirection.Ascending ? "ASC" : "DESC";
            var orderAttribute = querySortBy switch
            {
                QuerySortBy.DateCreated => "i.Created DESC",
                QuerySortBy.Popularity => "i.Views DESC, i.Created DESC",
                QuerySortBy.Comments => "i.CommentCount DESC, i.Created DESC",
                _ => null
            };

            // get the complete list of ids
            //var query = $"SELECT TOP @maxResults i.id, i.GalleryId FROM i WHERE CONTAINS(i.TagsCsv, @tag, true) ORDER BY {orderAttribute} {direction}";
            var query = $"SELECT TOP @maxResults i.id, i.GalleryId FROM i WHERE CONTAINS(i.TagsCsv, @tag, true) ORDER BY {orderAttribute}";
            var queryDefinition = new QueryDefinition(query).WithParameter("@maxResults", maxResults).WithParameter("@tag", tag);
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = container.GetItemQueryIterator<JObject>(queryDefinition);
            var ids = new List<DatabaseId>();
            double charge = 0;
            TimeSpan elapsedTime = default;

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                ids.AddRange(results.Select(result => new DatabaseId(result["id"].Value<string>(), result["GalleryId"].Value<string>())));
                charge += results.RequestCharge;
                elapsedTime += results.Diagnostics.GetClientElapsedTime();
            }

            Log.Debug($"ImageServer.GetImagesAsync(tag): Found {ids.Count} ids using query: {queryDefinition.QueryText}");
            Log.Debug($"ImageServer.GetImagesAsync(tag): Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            // now with all the ids we know how many total results there are and so can populate paging info
            var pagedResultSet = new PagedResultSet<Image>
            {
                PageSize = pageSize, 
                TotalResults = ids.Count, 
                CurrentPage = page,
                QuerySortBy = querySortBy,
                QueryRange = queryRange
            };

            if (page == 1 && pagedResultSet.TotalPages == 0)
                return pagedResultSet;

            // don't let users try and request a page that doesn't exist
            if (page > pagedResultSet.TotalPages)
                return null;

            if (ids.Count <= 0) 
                return pagedResultSet;

            // now just retrieve a page's worth of images from the results
            var offset = (page - 1) * pageSize;
            var itemsToGet = ids.Count >= pageSize ? pageSize : ids.Count;

            // if we're on the last page just get the remaining items
            if (page == pagedResultSet.TotalPages)
                itemsToGet = pagedResultSet.TotalResults - offset;

            var pageIds = ids.GetRange(offset, itemsToGet);
            foreach (var id in pageIds)
                pagedResultSet.Results.Add(await GetImageAsync(id.PartitionKey, id.Id));

            return pagedResultSet;
        }

        /// <summary>
        /// Returns a page of images that match a search term.
        /// </summary>
        /// <param name="term">The search term to use to search for galleries.</param>
        /// <param name="page">The page of galleries to return results from, for the first page use 1.</param>
        /// <param name="pageSize">The maximum number of galleries to return per page, i.e. 20.</param>
        /// <param name="maxResults">The maximum number of galleries to get paged results for, i.e. how many pages to look for.</param>
        /// <param name="includeInactiveGalleries">Indicates whether or not images in inactive (not active) galleries should be returned. False by default.</param>
        public async Task<PagedResultSet<Image>> SearchForImagesAsync(string term, int page = 1, int pageSize = 20, int maxResults = 500, bool includeInactiveGalleries = false)
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
                ? "SELECT TOP @maxResults i.id AS Id, i.GalleryId AS PartitionKey FROM i WHERE CONTAINS(i.Name, @term, true) OR CONTAINS(i.TagsCsv, @term, true) ORDER BY i.Created DESC"
                : "NOT CURRENTLY SUPPORTED - NO WAY TO TELL!";

            var queryDefinition = new QueryDefinition(queryText)
                .WithParameter("@maxResults", maxResults)
                .WithParameter("@term", term.ToLower());

            var databaseIds = await Server.GetIdsByQueryAsync(Constants.ImagesContainerName, queryDefinition);

            // now with all the ids we know how many total results there are and so can populate paging info
            var pagedResultSet = new PagedResultSet<Image> { PageSize = pageSize, TotalResults = databaseIds.Count, CurrentPage = page };

            // don't let users try and request a page that doesn't exist
            if (page > pagedResultSet.TotalPages)
                page = pagedResultSet.TotalPages;

            // now just retrieve a page's worth of images from the results
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
            //    pagedResultSet.Results.Add(await GetImageAsync(id.PartitionKey, id.Id));

            var unorderedImages = new List<Image>();
            var tasks = new List<Task>();
            foreach (var pageId in pageIds)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var i = await GetImageAsync(pageId.PartitionKey, pageId.Id);
                    lock (unorderedImages)
                        unorderedImages.Add(i);
                }));
            }

            var t = Task.WhenAll(tasks);
            t.Wait();

            // put the unordered images into the results list in the same order as the ids were retrieved from the db
            foreach (var id in pageIds)
                pagedResultSet.Results.Add(unorderedImages.SingleOrDefault(i => i.Id == id.Id));

            return pagedResultSet;
        }

        /// <summary>
        /// Causes an Image to be permanently deleted from storage and database.
        /// Will result in some images being re-ordered to avoid holes in positions.
        /// </summary>
        /// <param name="image">The Image to be deleted.</param>
        /// <param name="isGalleryBeingDeleted">Will disable all re-ordering and gallery thumbnail tasks if the gallery is being deleted.</param>
        public async Task DeleteImageAsync(Image image, bool isGalleryBeingDeleted = false)
        {
            await DeleteImageFilesAsync(image);

            // make note of the image position and gallery id as we might have to re-order photos
            var position = image.Position;
            var galleryId = image.GalleryId;

            // finally, delete the database record
            var imagesContainer = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var deleteResponse = await imagesContainer.DeleteItemAsync<Image>(image.Id, new PartitionKey(image.GalleryId));
            Log.Debug($"ImageServer:DeleteImageAsync: Request charge: {deleteResponse.RequestCharge}. Elapsed time: {deleteResponse.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

            // if necessary, re-order photos down-position from where the deleted photo used to be
            if (!isGalleryBeingDeleted)
            {
                if (position.HasValue)
                {
                    Log.Debug("ImageServer:DeleteImageAsync: Image had an order, re-ordering subsequent images...");

                    // get the ids of images that have a position down from where our deleted image used to be
                    var queryDefinition = new QueryDefinition("SELECT c.id AS Id, c.GalleryId AS PartitionKey FROM c WHERE c.GalleryId = @galleryId AND c.Position > @position ORDER BY c.Position")
                        .WithParameter("@galleryId", galleryId)
                        .WithParameter("@position", position.Value);

                    var ids = await Server.GetIdsByQueryAsync(Constants.GalleriesContainerName, queryDefinition);
                    foreach (var databaseId in ids)
                    {
                        var affectedImage = await GetImageAsync(galleryId, databaseId.Id);

                        // this check shouldn't be required as if one image has a position then all should
                        // but life experience suggests it's best to be sure.
                        if (affectedImage.Position == null)
                            continue;

                        affectedImage.Position = position;
                        await UpdateImageAsync(affectedImage);
                        position += 1;
                    }
                }
                
                // do we need to update the gallery thumbnail after we delete this image?
                var gallery = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, image.GalleryId);
                var newThumbnailNeeded = gallery.ThumbnailFiles == null || gallery.ThumbnailFiles.OriginalId == image.Files.OriginalId;
                if (newThumbnailNeeded)
                {
                    var images = await GetGalleryImagesAsync(gallery.Id);
                    if (images.Count > 0)
                    {
                        var orderedImages = Utilities.OrderImages(images);
                        gallery.ThumbnailFiles = orderedImages.First().Files;
                        Log.Debug($"ImageServer.DeleteImageAsync: New gallery thumbnail was needed. Set to {gallery.ThumbnailFiles.Spec800Id}");
                    }
                    else
                    {
                        gallery.ThumbnailFiles = null;
                        Log.Debug("ImageServer.DeleteImageAsync: New gallery thumbnail was needed but no images to choose from.");
                    }

                    await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
                }
            }
        }

        /// <summary>
        /// Re-orders gallery image positions so that an image is at a new position.
        /// </summary>
        public async Task UpdateImagePositionAsync(string galleryId, string imageId, int position)
        {
            var images = Utilities.OrderImages(await GetGalleryImagesAsync(galleryId)).ToList();

            // remove the image being moved
            var imageBeingOrdered = images.SingleOrDefault(i => i.Id.Equals(imageId));
            if (imageBeingOrdered == null)
                throw new ArgumentException($"image '{imageId}' could not be found in the gallery '{galleryId}'.");

            images.Remove(imageBeingOrdered);
            images.Insert(position, imageBeingOrdered);

            // now re-position all images and update if the values are different
            var index = 0;
            foreach (var image in images)
            {
                if (index != image.Position)
                {
                    image.Position = index;
                    await UpdateImageAsync(image);
                    Log.Debug($"UpdateImagePositionAsync(): Updated image: {image.Id} with position {image.Position}");
                }
                else
                {
                    Log.Debug($"UpdateImagePositionAsync(): Image position didn't need updating: {image.Id} with position {image.Position}");
                }
                
                index++;
            }

            if (position == 0)
            {
                Log.Debug("ImageServer.UpdateImagePositionAsync: New position is 0, need to update gallery thumbnail...");
                var gallery = await Server.Instance.Galleries.GetGalleryAsync(imageBeingOrdered.GalleryCategoryId, imageBeingOrdered.GalleryId);
                gallery.ThumbnailFiles = imageBeingOrdered.Files;
                await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
            }
        }

        /// <summary>
        /// When a user views an image, we increase the count as part of determining how popular an image is.
        /// </summary>
        public async Task IncreaseImageViewsAsync(string galleryId, string imageId)
        {
            var i = await GetImageAsync(galleryId, imageId);
            i.Views += 1;
            await UpdateImageAsync(i);
        }

        /// <summary>
        /// Attempts to return the image previous to a given image in terms of the order they are shown in their gallery. May return null.
        /// </summary>
        public async Task<Image> GetPreviousImageInGalleryAsync(Image currentImage)
        {
            // choose the right query - not all galleries will have had their images ordered. fall back to image creation date for unordered galleries
            var query = currentImage.Position.HasValue
                ? new QueryDefinition("SELECT TOP 1 VALUE c.id FROM c WHERE c.GalleryId = @galleryId AND c.Position < @position ORDER BY c.Position DESC")
                    .WithParameter("@galleryId", currentImage.GalleryId)
                    .WithParameter("@position", currentImage.Position.Value)
                : new QueryDefinition("SELECT TOP 1 VALUE c.id FROM c WHERE c.GalleryId = @galleryId AND c.Created < @created ORDER BY c.Created DESC")
                    .WithParameter("@galleryId", currentImage.GalleryId)
                    .WithParameter("@created", currentImage.Created);

            var id = await GetImageIdByQueryAsync(query);
            return await GetImageAsync(currentImage.GalleryId, id);
        }

        /// <summary>
        /// Attempts to return the image after to a given image in terms of the order they are shown in their gallery. May return null.
        /// </summary>
        public async Task<Image> GetNextImageInGalleryAsync(Image currentImage)
        {
            // choose the right query - not all galleries will have had their images ordered. fall back to image creation date for unordered galleries
            var query = currentImage.Position.HasValue
                ? new QueryDefinition("SELECT TOP 1 VALUE c.id FROM c WHERE c.GalleryId = @galleryId AND c.Position > @position ORDER BY c.Position")
                    .WithParameter("@galleryId", currentImage.GalleryId)
                    .WithParameter("@position", currentImage.Position.Value)
                : new QueryDefinition("SELECT TOP 1 VALUE c.id FROM c WHERE c.GalleryId = @galleryId AND c.Created > @created ORDER BY c.Created")
                    .WithParameter("@galleryId", currentImage.GalleryId)
                    .WithParameter("@created", currentImage.Created);

            var id = await GetImageIdByQueryAsync(query);
            return await GetImageAsync(currentImage.GalleryId, id);
        }

        public async Task CreateCommentAsync(string comment, string userId, bool receiveNotifications, string galleryId, string imageId)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var imageComment = new Comment
            {
                CreatedByUserId = userId,
                Text = comment.Trim()
            };

            image.Comments.Add(imageComment);
            image.CommentCount++;

            // subscribe the user to comment notifications if they've asked to be
            if (receiveNotifications)
            {
                // create a comment subscription
                if (!image.UserCommentSubscriptions.Contains(userId))
                    image.UserCommentSubscriptions.Add(userId);

                // todo: have something async subscribe to new comments and send out notifications as needed
                // todo: later on limit how many notifications a user gets for a single object

                // add message to queue for notifications

                // notification sender needs to know:
                // - object id 1 (i.e. gallery id)
                // - object id 2 (i.e. image id
                // - object type
                // - when they commented
                // i.e. 12,13,image,01/01/2021 18:54:00

                // create the message and send to the Azure Storage notifications queue
                var message = $"{galleryId}:{imageId}:image:{imageComment.Created.Ticks}";
                var encodedMessage = Utilities.Base64Encode(message);
                await Server.Instance.NotificationProcessingQueueClient.SendMessageAsync(encodedMessage);
            }

            await UpdateImageAsync(image);

            // update the gallery total comment count too
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, image.GalleryId);
            gallery.CommentCount++;
            await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
        }

        public async Task DeleteCommentAsync(Gallery gallery, Image image, Comment comment)
        {
            var removed = image.Comments.Remove(comment);
            if (removed)
            {
                image.CommentCount--;
                await Server.Instance.Images.UpdateImageAsync(image);

                gallery.CommentCount--;
                await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
            }
            else
            {
                Log.Information($"GalleryServer.DeleteCommentAsync(): No comment removed. imageId={image.Id}, commentCreatedTicks={comment.Created.Ticks}, commentCreatedByUserId={comment.CreatedByUserId}");
            }
        }
        #endregion

        #region admin methods
        /// <summary>
        /// After deleting pre-generated image files you can use this method to regenerate image files for a whole gallery of images.
        /// </summary>
        /// <param name="galleryId">The unique identifier for the gallery to generate image files for.</param>
        public async Task RegenerateImageFiles(string galleryId)
        {
            if (string.IsNullOrEmpty(galleryId))
                throw new ArgumentException("galleryId is invalid", nameof(galleryId));

            var images = await GetGalleryImagesAsync(galleryId);
            foreach (var image in images)
                await PostProcessImagesAsync(image);
        }

        /// <summary>
        /// Deletes all pre-generated image files and updates the image object.
        /// </summary>
        public async Task DeletePreGenImageFilesAsync(string categoryId, string galleryId)
        {
            // delete the files
            var images = await GetGalleryImagesAsync(galleryId);
            var tasks = images.Select(HandleDeletePreGenImagesAsync).ToList();
            await Task.WhenAll(tasks);

            // clear the gallery thumbnails references
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            gallery.ThumbnailFiles = null;
            await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
        }

        /// <summary>
        /// Causes the metadata on an image to be re-inspected and then any relevant properties on the Image updated.
        /// </summary>
        public async Task ReprocessImageMetadataAsync(Image image)
        {
            // create the message and send to the Azure Storage queue
            // message format: {operation}:{image_id}:{gallery_id}:{gallery_category_id}:{overwrite_image_properties}
            var ids = $"{WorkerOperation.ReprocessMetadata}:{image.Id}:{image.GalleryId}:{image.GalleryCategoryId}:true";
            Log.Debug($"ReprocessImageMetadataAsync() - Sending message (pre-base64 encoding): {ids}");

            var messageText = Utilities.Base64Encode(ids);
            await Server.Instance.ImageProcessingQueueClient.SendMessageAsync(messageText);
        }
        #endregion

        #region internal methods
        internal async Task<List<Image>> GetImagesUserHasCommentedOnAsync(string userId)
        {
            const string query = "SELECT * FROM c WHERE c.Comments.CreatedByUserId = @userId";
            var queryDefinition = new QueryDefinition(query).WithParameter("@userId", userId);
            return await GetImagesByQueryAsync(queryDefinition);
        }

        internal async Task<int> GetImagesScalarByQueryAsync(QueryDefinition queryDefinition)
        {
            if (queryDefinition == null)
                throw new InvalidOperationException("queryDefinition is null");

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var result = container.GetItemQueryIterator<object>(queryDefinition);

            const int count = 0;
            if (!result.HasMoreResults)
                return count;

            var resultSet = await result.ReadNextAsync();
            Log.Debug($"ImageServer.GetImagesScalarByQueryAsync: Query: {queryDefinition.QueryText}");
            Log.Debug($"ImageServer.GetImagesScalarByQueryAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

            if (resultSet.Resource == null || !resultSet.Resource.Any())
                return -1;

            return Convert.ToInt32(resultSet.Resource.First());
        }
        #endregion

        #region private methods
        /// <summary>
        /// Deletes the original image and any generated images for an Image.
        /// </summary>
        private static async Task DeleteImageFilesAsync(Image image, bool clearImageFileReferences = false)
        {
            // delete all image files
            var deleteTasks = new List<Task>
            {
                DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.SpecOriginal)),
                DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.Spec3840)),
                DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.Spec2560)),
                DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.Spec1920)),
                DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.Spec800)),
                DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.SpecLowRes))
            };
            await Task.WhenAll(deleteTasks);

            if (clearImageFileReferences)
            {
                image.Files.OriginalId = null;
                image.Files.Spec3840Id = null;
                image.Files.Spec2560Id = null;
                image.Files.Spec1920Id = null;
                image.Files.Spec800Id = null;
                image.Files.SpecLowResId = null;
            }
        }

        private async Task HandleDeletePreGenImagesAsync(Image image)
        {
            await DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.Spec3840));
            await DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.Spec2560));
            await DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.Spec1920));
            await DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.Spec800));
            await DeleteImageFileAsync(image, ImageFileSpecs.GetImageFileSpec(FileSpec.SpecLowRes));

            image.Files.Spec3840Id = null;
            image.Files.Spec2560Id = null;
            image.Files.Spec1920Id = null;
            image.Files.Spec800Id = null;
            image.Files.SpecLowResId = null;

            await UpdateImageAsync(image);
        }

        private static async Task<List<Image>> GetImagesByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = container.GetItemQueryIterator<Image>(queryDefinition);
            var users = new List<Image>();
            double charge = 0;
            TimeSpan elapsedTime = default;

            while (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                charge += resultSet.RequestCharge;
                elapsedTime += resultSet.Diagnostics.GetClientElapsedTime();
                users.AddRange(resultSet);
            }

            Log.Debug($"ImageServer.GetImagesByQueryAsync: Query: {queryDefinition.QueryText}");
            Log.Debug($"ImageServer.GetImagesByQueryAsync: Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            return users;
        }

        private static async Task<Image> GetImageByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = container.GetItemQueryIterator<Image>(queryDefinition);

            if (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                foreach (var image in resultSet)
                {
                    Log.Debug("GetImageByQueryAsync: Found a gallery using query: " + queryDefinition.QueryText);
                    Log.Debug($"GetImageByQueryAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
                    return image;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves the ID of an image via a query you provide.
        /// </summary>
        /// <param name="queryDefinition">The query text must use the VALUE keyword to return just a singular (scalar) value.</param>
        private static async Task<string> GetImageIdByQueryAsync(QueryDefinition queryDefinition)
        {
            if (queryDefinition == null)
                throw new InvalidOperationException("queryDefinition is null");

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var result = container.GetItemQueryIterator<object>(queryDefinition);

            if (!result.HasMoreResults)
                return null;

            var resultSet = await result.ReadNextAsync();
            Log.Debug($"ImageServer.GetImageIdByQueryAsync: Query: {queryDefinition.QueryText}");
            Log.Debug($"ImageServer.GetImageIdByQueryAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

            return (string)resultSet.Resource.FirstOrDefault();
        }

        /// <summary>
        /// Handles deleting a specific version of an image file according to file spec.
        /// </summary>
        private static async Task DeleteImageFileAsync(Image image, ImageFileSpec imageFileSpec)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            var storageId = imageFileSpec.FileSpec switch
            {
                FileSpec.SpecOriginal => image.Files.OriginalId,
                FileSpec.Spec3840 => image.Files.Spec3840Id,
                FileSpec.Spec2560 => image.Files.Spec2560Id,
                FileSpec.Spec1920 => image.Files.Spec1920Id,
                FileSpec.Spec800 => image.Files.Spec800Id,
                FileSpec.SpecLowRes => image.Files.SpecLowResId,
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(storageId))
            {
                var container = Server.Instance.BlobServiceClient.GetBlobContainerClient(imageFileSpec.ContainerName);
                var response = await container.DeleteBlobIfExistsAsync(storageId, DeleteSnapshotsOption.IncludeSnapshots);
                Log.Debug("ImageServer.DeleteImageFileAsync: response status: " + response.Value);
                return;
            }

            Log.Debug("ImageServer.DeleteImageFileAsync: storage id is null. FileSpec: " + imageFileSpec.FileSpec);
        }

        /// <summary>
        /// Adds the image id to the images-to-process Azure storage message queue so that pre-generated images can be created to speed up page delivery.
        /// </summary>
        private static async Task PostProcessImagesAsync(Image image)
        {
            // create the message and send to the Azure Storage queue
            // message format: {operation}:{image_id}:{gallery_id}:{gallery_category_id}:{overwrite_image_properties}
            var ids = $"{WorkerOperation.Process}:{image.Id}:{image.GalleryId}:{image.GalleryCategoryId}:true";
            Log.Debug($"PostProcessImagesAsync() - Sending message (pre-base64 encoding): {ids}");

            var messageText = Utilities.Base64Encode(ids);
            await Server.Instance.ImageProcessingQueueClient.SendMessageAsync(messageText);
        }

        /// <summary>
        /// Checks if an uploaded image meets the minimum dimensions. Will throw a ImageTooSmallException if not.
        /// </summary>
        private static void CheckImageDimensions(Stream imageStream)
        {
            var image = System.Drawing.Image.FromStream(imageStream);
            var orientation = image.Width >= image.Height
                ? ImageOrientation.Landscape
                : ImageOrientation.Portrait;

            var imageTooSmallErrorMessage = $"Image must be equal or bigger than 800px on the longest side. Detected size {image.Width}x{image.Height}";
            switch (orientation)
            {
                case ImageOrientation.Landscape when image.Width < 800:
                    throw new ImageTooSmallException(imageTooSmallErrorMessage);
                case ImageOrientation.Portrait when image.Height < 800:
                    throw new ImageTooSmallException(imageTooSmallErrorMessage);
            }

            imageStream.Position = 0;
        }
        #endregion
    }
}
