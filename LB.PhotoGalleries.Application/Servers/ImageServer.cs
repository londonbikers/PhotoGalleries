using Azure.Storage.Blobs.Models;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Models.Exceptions;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using MetadataExtractor.Formats.Iptc;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MetadataExtractor.Formats.Xmp;
using Directory = MetadataExtractor.Directory;
using Image = LB.PhotoGalleries.Models.Image;

namespace LB.PhotoGalleries.Application.Servers
{
    public class ImageServer
    {
        #region constructors
        internal ImageServer()
        {
        }
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

                if (image != null)
                {
                    // the Image already exists but may not be sufficiently populated...
                    if (!image.Id.HasValue())
                        image.Id = Utilities.GenerateId();
                    if (!image.Name.HasValue())
                        image.Name = TidyImageName(Path.GetFileNameWithoutExtension(filename));
                    if (!image.GalleryCategoryId.HasValue())
                        image.GalleryCategoryId = galleryCategoryId;
                    if (!image.GalleryId.HasValue())
                        image.GalleryId = galleryId;
                    if (!image.Files.OriginalId.HasValue())
                        image.Files.OriginalId = image.Id + Path.GetExtension(filename).ToLower();
                }
                else
                {
                    // create the Image object anew
                    var id = Utilities.GenerateId();
                    image = new Image
                    {
                        Id = id,
                        Name = TidyImageName(Path.GetFileNameWithoutExtension(filename)),
                        GalleryCategoryId = galleryCategoryId,
                        GalleryId = galleryId,
                        Files = { OriginalId = id + Path.GetExtension(filename).ToLower() }
                    };
                }

                ParseAndAssignImageMetadata(image, imageStream, performImageDimensionsCheck);

                if (!image.IsValid())
                    throw new InvalidOperationException("Image would be invalid. PLease check all required properties are set.");

                // upload the original file to storage
                var originalContainerClient = Server.Instance.BlobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
                await originalContainerClient.UploadBlobAsync(image.Files.OriginalId, imageStream);

                // create the database record
                var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
                var response = await container.CreateItemAsync(image, new PartitionKey(image.GalleryId));
                Debug.WriteLine($"ImageServer.CreateImageAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

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
            Debug.WriteLine($"ImageServer.UpdateImageAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
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

            Debug.WriteLine($"ImageServer.GetGalleryImagesAsync: Found {images.Count} gallery images");
            Debug.WriteLine($"ImageServer.GetGalleryImagesAsync: Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            return images;
        }

        public async Task<Image> GetImageAsync(string galleryId, string imageId)
        {
            if (string.IsNullOrEmpty(galleryId) || string.IsNullOrEmpty(imageId))
            {
                Debug.WriteLine("ImageServer:GetImageAsync: some args were null, returning null");
                return null;
            }

            try
            {
                var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
                var response = await container.ReadItemAsync<Image>(imageId, new PartitionKey(galleryId));
                Debug.WriteLine($"ImageServer:GetImageAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
                return response.Resource;
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    Debug.WriteLine($"Image not found in db. GalleryId {galleryId}, ImageId {imageId}");
                    return null;
                }

                // some other unexpected exception
                throw;
            }
        }

        /// <summary>
        /// Returns a page of images with a specific tag
        /// </summary>
        /// <param name="tag">The tag used to find images for.</param>
        /// <param name="page">The page of galleries to return results from, for the first page use 1.</param>
        /// <param name="pageSize">The maximum number of galleries to return per page, i.e. 20.</param>
        /// <param name="maxResults">The maximum number of galleries to get paged results for, i.e. how many pages to look for.</param>
        public async Task<PagedResultSet<Image>> GetImagesForTagAsync(string tag, int page = 1, int pageSize = 20, int maxResults = 500)
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

            // get the complete list of ids
            var queryDefinition = new QueryDefinition("SELECT TOP @maxResults i.id, i.GalleryId FROM i WHERE CONTAINS(i.TagsCsv, @tag, true) ORDER BY i.Created DESC")
                    .WithParameter("@maxResults", maxResults)
                    .WithParameter("@tag", tag);
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

            Debug.WriteLine($"ImageServer.GetImagesAsync(tag): Found {ids.Count} ids using query: {queryDefinition.QueryText}");
            Debug.WriteLine($"ImageServer.GetImagesAsync(tag): Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            // now with all the ids we know how many total results there are and so can populate paging info
            var pagedResultSet = new PagedResultSet<Image> { PageSize = pageSize, TotalResults = ids.Count, CurrentPage = page };

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

            // make note of the image position and gallery id as we might have to re-order photos
            var position = image.Position;
            var galleryId = image.GalleryId;

            // finally, delete the database record
            var imagesContainer = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var deleteResponse = await imagesContainer.DeleteItemAsync<Image>(image.Id, new PartitionKey(image.GalleryId));
            Debug.WriteLine($"ImageServer:DeleteImageAsync: Request charge: {deleteResponse.RequestCharge}. Elapsed time: {deleteResponse.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

            // if necessary, re-order photos down-position from where the deleted photo used to be
            if (!isGalleryBeingDeleted)
            {
                if (position.HasValue)
                {
                    Debug.WriteLine("ImageServer:DeleteImageAsync: Image had an order, re-ordering subsequent images...");

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
                var newThumbnailNeeded = gallery.ThumbnailFiles.OriginalId == image.Files.OriginalId;

                if (newThumbnailNeeded)
                {
                    var images = await GetGalleryImagesAsync(gallery.Id);
                    if (images.Count > 0)
                    {
                        var orderedImages = Utilities.OrderImages(images);
                        gallery.ThumbnailFiles = orderedImages.First().Files;
                        Debug.WriteLine($"ImageServer.DeleteImageAsync: New gallery thumbnail was needed. Set to {gallery.ThumbnailFiles.Spec800Id}");
                    }
                    else
                    {
                        gallery.ThumbnailFiles = null;
                        Debug.WriteLine("ImageServer.DeleteImageAsync: New gallery thumbnail was needed but no images to choose from.");
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
            var images = await GetGalleryImagesAsync(galleryId);

            // migrated galleries and galleries that have been ordered before will have position values set on images.
            // but galleries that have just been uploaded normally will not have position values set on images.
            // in this scenario we need to set a position on the images first as they are retrieved from the database and then re-order.
            
            var hadToPerformInitialOrdering = false;
            if (images.Any(i => i.Position.HasValue == false))
            {
                // perform initial ordering of images
                for (var i = 0; i < images.Count; i++)
                    images[i].Position = i;

                hadToPerformInitialOrdering = true;
            }

            // now we can re-order the images
            // cut out the image being moved first then bump up a position each image from where our image will go
            var imageBeingOrdered = images.Single(i => i.Id == imageId);
            images.RemoveAll(i => i.Id == imageId);

            var newPosition = position + 1;
            foreach (var image in images.Where(image => image.Position >= position))
            {
                image.Position = newPosition;
                newPosition += 1;
            }

            // now set the desired image with the desired position and add it back in to the list
            imageBeingOrdered.Position = position;
            images.Add(imageBeingOrdered);

            // now write the new positions back to the database
            // we have an opportunity to introduce some efficiency, i.e. if we are moving an image from the back to the middle then
            // we don't need to re-order images at the start. this will save on db interactions.
            var imagesToUpdate = hadToPerformInitialOrdering ? images : images.Where(i => i.Position >= position);
            foreach (var image in imagesToUpdate)
                await UpdateImageAsync(image);

            if (position == 0)
            {
                Debug.WriteLine("ImageServer.UpdateImagePositionAsync: New position is 0, need to update gallery thumbnail...");
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
            Debug.WriteLine($"ImageServer.GetImagesScalarByQueryAsync: Query: {queryDefinition.QueryText}");
            Debug.WriteLine($"ImageServer.GetImagesScalarByQueryAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

            if (resultSet.Resource == null || !resultSet.Resource.Any())
                return -1;

            return Convert.ToInt32(resultSet.Resource.First());
        }
        #endregion

        #region private methods
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

            Debug.WriteLine($"ImageServer.GetImagesByQueryAsync: Query: {queryDefinition.QueryText}");
            Debug.WriteLine($"ImageServer.GetImagesByQueryAsync: Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            return users;
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
            Debug.WriteLine($"ImageServer.GetImageIdByQueryAsync: Query: {queryDefinition.QueryText}");
            Debug.WriteLine($"ImageServer.GetImageIdByQueryAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

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
                Debug.WriteLine("ImageServer.DeleteImageFileAsync: response status: " + response.Value);
                return;
            }

            Debug.WriteLine("ImageServer.DeleteImageFileAsync: storage id is null. FileSpec: " + imageFileSpec.FileSpec);
        }

        /// <summary>
        /// Adds the image id to the images-to-process Azure storage message queue so that pre-generated images can be created to speed up page delivery.
        /// </summary>
        private static async Task PostProcessImagesAsync(Image image)
        {
            // create the message and send to the Azure Storage queue
            var ids = $"{image.Id}:{image.GalleryId}:{image.GalleryCategoryId}";
            var messageText = Utilities.Base64Encode(ids);
            await Server.Instance.ImageProcessingQueueClient.SendMessageAsync(messageText);
        }

        private static string TidyImageName(string name)
        {
            name = name.Replace("_", " ");
            name = Regex.Replace(name, " {2,}", " ", RegexOptions.Compiled);
            return name;
        }
        #endregion

        #region metadata parsing methods
        /// <summary>
        /// Images contain metadata that describes the photo to varying degrees. This method extracts the metadata
        /// and parses out the most interesting pieces we're interested in and assigns it to the image object so we can
        /// present the information to the user and use it to help with searches.
        /// </summary>
        /// <param name="image">The Image object to assign the metadata to.</param>
        /// <param name="imageStream">The stream containing the recently-uploaded image file to inspect for metadata.</param>
        /// <param name="performImageDimensionsCheck">Ordinarily images must be bigger than 800x800 in size but for migration purposes we might want to override this.</param>
        private static void ParseAndAssignImageMetadata(Image image, Stream imageStream, bool performImageDimensionsCheck)
        {
            // whilst image dimensions can be extracted from metadata in some cases, not in every case and this isn't acceptable
            using var bm = new Bitmap(imageStream);
            image.Metadata.Width = bm.Width;
            image.Metadata.Height = bm.Height;

            var orientation = image.Metadata.Width.Value >= image.Metadata.Height.Value
                ? ImageOrientation.Landscape
                : ImageOrientation.Portrait;

            var imageTooSmallErrorMessage = $"Image must be equal or bigger than 800px on the longest side. Detected size {image.Metadata.Width}x{image.Metadata.Height}";
            if (performImageDimensionsCheck)
            {
                switch (orientation)
                {
                    case ImageOrientation.Landscape when image.Metadata.Width < 800:
                        throw new ImageTooSmallException(imageTooSmallErrorMessage);
                    case ImageOrientation.Portrait when image.Metadata.Height < 800:
                        throw new ImageTooSmallException(imageTooSmallErrorMessage);
                }
            }

            if (imageStream.CanSeek && imageStream.Position != 0)
                imageStream.Position = 0;

            var directories = ImageMetadataReader.ReadMetadata(imageStream);

            // debug info
            // ----------------------------------------------------------------------------------
            //Debug.WriteLine("-------------------------------------------------");
            //Debug.WriteLine("");
            //foreach (var directory in directories)
            //{
            //    // Each directory stores values in tags
            //    foreach (var tag in directory.Tags)
            //        Debug.WriteLine(tag);

            //    // Each directory may also contain error messages
            //    foreach (var error in directory.Errors)
            //        Debug.WriteLine("ERROR: " + error);
            //}
            //Debug.WriteLine("");
            //Debug.WriteLine("-------------------------------------------------");
            // ----------------------------------------------------------------------------------

            image.Metadata.TakenDate = GetImageDateTaken(directories);

            var iso = GetImageIso(directories);
            if (iso.HasValue)
                image.Metadata.Iso = iso.Value;

            if (!image.Credit.HasValue())
            {
                var credit = GetImageCredit(directories);
                if (credit.HasValue())
                    image.Credit = credit;
            }

            var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (exifIfd0Directory != null)
            {
                var make = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagMake);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (make != null && make.Description.HasValue())
                    image.Metadata.CameraMake = make.Description;

                var model = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagModel);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (model != null && model.Description.HasValue())
                    image.Metadata.CameraModel = model.Description;

                if (!image.Caption.HasValue())
                {
                    var imageDescription = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagImageDescription);
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                    if (imageDescription != null && imageDescription.Description.HasValue())
                        image.Caption = imageDescription.Description;
                }
            }

            var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfdDirectory != null)
            {
                var exposureTime = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagExposureTime);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (exposureTime != null && exposureTime.Description.HasValue())
                    image.Metadata.ExposureTime = exposureTime.Description;

                var aperture = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagAperture);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (aperture != null && aperture.Description.HasValue())
                    image.Metadata.Aperture = aperture.Description;

                var exposureBias = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagExposureBias);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (exposureBias != null && exposureBias.Description.HasValue())
                    image.Metadata.ExposureBias = exposureBias.Description;

                var meteringMode = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagMeteringMode);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (meteringMode != null && meteringMode.Description.HasValue())
                    image.Metadata.MeteringMode = meteringMode.Description;

                var flash = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagFlash);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (flash != null && flash.Description.HasValue())
                    image.Metadata.Flash = flash.Description;

                var focalLength = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagFocalLength);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (focalLength != null && focalLength.Description.HasValue())
                    image.Metadata.FocalLength = focalLength.Description;

                var lensMake = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagLensMake);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (lensMake != null && lensMake.Description.HasValue())
                    image.Metadata.LensMake = lensMake.Description;

                var lensModel = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagLensModel);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (lensModel != null && lensModel.Description.HasValue())
                    image.Metadata.LensModel = lensModel.Description;

                var whiteBalance = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagWhiteBalance);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (whiteBalance != null && whiteBalance.Description.HasValue())
                    image.Metadata.WhiteBalance = whiteBalance.Description;

                var whiteBalanceMode = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagWhiteBalanceMode);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (whiteBalanceMode != null && whiteBalanceMode.Description.HasValue())
                    image.Metadata.WhiteBalanceMode = whiteBalanceMode.Description;
            }

            var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();
            var location = gpsDirectory?.GetGeoLocation();
            if (location != null)
            {
                image.Metadata.LocationLatitude = location.Latitude;
                image.Metadata.LocationLongitude = location.Longitude;
            }

            var iptcDirectory = directories.OfType<IptcDirectory>().FirstOrDefault();
            if (iptcDirectory != null)
            {
                if (!image.Name.HasValue())
                {
                    var objectName = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagObjectName);
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                    if (objectName != null && objectName.Description.HasValue())
                        image.Name = TidyImageName(objectName.Description);
                }

                var keywords = iptcDirectory.GetKeywords();
                if (keywords != null)
                    foreach (var keyword in keywords.Where(k => k.HasValue()))
                        image.TagsCsv = Utilities.AddTagToCsv(image.TagsCsv, keyword);
            }

            var xmpDirectory = directories.OfType<XmpDirectory>().FirstOrDefault();
            if (xmpDirectory != null)
            {
                // see if there's any tags (subjects in xmp)
                var subjects = xmpDirectory.XmpMeta.Properties.Where(q => q.Path != null && q.Path.StartsWith("dc:subject") && !string.IsNullOrEmpty(q.Value));
                foreach (var subject in subjects)
                    image.TagsCsv = Utilities.AddTagToCsv(image.TagsCsv, subject.Value);

                // sometimes we have no camera in, but some can be inferred from lens profile info
                var lensProfileFilename = xmpDirectory.XmpMeta.GetPropertyString("http://ns.adobe.com/camera-raw-settings/1.0/", "crs:LensProfileFilename");
                if (!string.IsNullOrEmpty(lensProfileFilename) && string.IsNullOrEmpty(image.Metadata.CameraModel))
                {
                    var camera = lensProfileFilename.Substring(0, lensProfileFilename.IndexOf(" (", StringComparison.Ordinal));
                    image.Metadata.CameraModel = camera;
                }
                
                var lensProfileName = xmpDirectory.XmpMeta.GetPropertyString("http://ns.adobe.com/camera-raw-settings/1.0/", "crs:LensProfileName");
                if (!string.IsNullOrEmpty(lensProfileName) && string.IsNullOrEmpty(image.Metadata.LensModel))
                {
                    var lens = Regex.Match(lensProfileName, @"\((.*?)\)", RegexOptions.Compiled);
                    if (lens.Success && lens.Value.Contains("(") && lens.Value.Contains(")"))
                        image.Metadata.LensModel = lens.Value.TrimStart('(').TrimEnd(')');
                }
            }

            // wind the stream back to allow other code to work with the stream
            imageStream.Position = 0;
        }

        /// <summary>
        /// Attempts to extract and parse image date capture information from image metadata.
        /// </summary>
        /// <param name="directories">Metadata directories extracted from the image stream.</param>
        private static DateTime? GetImageDateTaken(IEnumerable<Directory> directories)
        {
            // obtain the Exif SubIFD directory
            var directory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (directory == null)
                return null;

            // query the tag's value
            if (directory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateTime))
                return dateTime;

            return null;
        }

        /// <summary>
        /// Attempts to extract image iso information from image metadata.
        /// </summary>
        /// <param name="directories">Metadata directories extracted from the image stream.</param>
        public static int? GetImageIso(IEnumerable<Directory> directories)
        {
            int iso;
            var enumerable = directories as Directory[] ?? directories.ToArray();
            var exifSubIfdDirectory = enumerable.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfdDirectory != null)
            {
                var isoTag = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagIsoEquivalent);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (isoTag != null && isoTag.Description.HasValue())
                {
                    var validIso = int.TryParse(isoTag.Description, out iso);
                    if (validIso)
                        return iso;

                    Debug.WriteLine($"ImageServer.GetImageIso: ExifSubIfdDirectory iso tag value wasn't an int: '{isoTag.Description}'");
                }
            }

            var nikonDirectory = enumerable.OfType<NikonType2MakernoteDirectory>().FirstOrDefault();
            // ReSharper disable once InvertIf
            if (nikonDirectory != null)
            {
                var isoTag = nikonDirectory.Tags.SingleOrDefault(t => t.Type == NikonType2MakernoteDirectory.TagIso1);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (isoTag == null || !isoTag.Description.HasValue())
                    return null;

                if (isoTag.Description == null || !isoTag.Description.StartsWith("ISO ")) 
                    return null;

                var isoTagProcessed = isoTag.Description.Split(' ')[1];
                var validIso = int.TryParse(isoTagProcessed, out iso);
                if (validIso)
                    return iso;

                Debug.WriteLine($"ImageServer.GetImageIso: NikonType2MakernoteDirectory iso tag value wasn't an int: '{isoTag.Description}'");
            }

            return null;
        }

        /// <summary>
        /// Attempts to extract image credit information from image metadata.
        /// </summary>
        /// <param name="directories">Metadata directories extracted from the image stream.</param>
        public static string GetImageCredit(IReadOnlyList<Directory> directories)
        {
            var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (exifIfd0Directory != null)
            {
                var creditTag = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagCopyright);
                if (creditTag != null && creditTag.Description.HasValue())
                    return creditTag.Description;
            }

            var iptcDirectory = directories.OfType<IptcDirectory>().FirstOrDefault();
            if (iptcDirectory != null)
            {
                var creditTag = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagCredit);
                if (creditTag != null && creditTag.Description.HasValue())
                    return creditTag.Description;
            }

            var xmpDirectory = directories.OfType<XmpDirectory>().FirstOrDefault();
            if (xmpDirectory?.XmpMeta != null)
            {
                var creator = xmpDirectory.XmpMeta.Properties.SingleOrDefault(q => q.Path != null && q.Path.Equals("dc:creator[1]", StringComparison.CurrentCultureIgnoreCase) && !string.IsNullOrEmpty(q.Value));
                if (creator != null)
                    return creator.Value;

                var rights = xmpDirectory.XmpMeta.Properties.SingleOrDefault(q => q.Path != null && q.Path.Equals("dc:rights[1]", StringComparison.CurrentCultureIgnoreCase) && !string.IsNullOrEmpty(q.Value));
                if (rights != null)
                    return rights.Value;
            }

            return null;
        }
        #endregion
    }
}
