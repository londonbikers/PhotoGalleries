using Azure.Storage.Blobs;
using Imageflow.Fluent;
using LB.PhotoGalleries.Application.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using MetadataExtractor.Formats.Iptc;
using Meziantou.Framework;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Directory = MetadataExtractor.Directory;
using Image = LB.PhotoGalleries.Application.Models.Image;

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
        public async Task CreateImageAsync(string galleryCategoryId, string galleryId, Stream imageStream, string filename)
        {
            try
            {
                if (string.IsNullOrEmpty(galleryId))
                    throw new ArgumentNullException(nameof(galleryId));

                if (imageStream == null)
                    throw new ArgumentNullException(nameof(imageStream));

                if (string.IsNullOrEmpty(filename))
                    throw new ArgumentNullException(nameof(filename));

                // create the Image object
                // note: we don't set a position as there's no efficient way to do this at this point. instead
                // we let the clients order by created date initially and then when/if a photographer orders the photos
                // then the position attribute is used to order images.
                var id = Utilities.GenerateId();
                var image = new Image
                {
                    Id = id,
                    Name = Path.GetFileNameWithoutExtension(filename),
                    GalleryCategoryId = galleryCategoryId,
                    GalleryId = galleryId
                };

                ParseAndAssignImageMetadata(image, imageStream);

                if (!image.IsValid())
                    throw new InvalidOperationException("Image would be invalid. PLease check all required properties are set.");

                // upload the original file to storage
                var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
                var originalContainerClient = blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
                await originalContainerClient.UploadBlobAsync(image.Files.OriginalId, imageStream);

                // create the small image we use for thumbnails at this point as we'll need it straight away (the rest can be created asynchronously)
                var imageBytes = Utilities.ConvertStreamToBytes(imageStream);
                await GenerateAndStoreImageFileAsync(image, FileSpec.Spec800, imageBytes, blobServiceClient);

                // create the database record
                var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
                var response = await container.CreateItemAsync(image, new PartitionKey(image.GalleryId));
                Debug.WriteLine($"ImageServer.CreateImageAsync: Request charge: {response.RequestCharge}");

                // was this the first image? set the gallery thumbnail using this image if so
                var gallery = await Server.Instance.Galleries.GetGalleryAsync(galleryCategoryId, galleryId);
                if (string.IsNullOrEmpty(gallery.ThumbnailStorageId))
                {
                    gallery.ThumbnailStorageId = image.Files.Spec800Id;
                    Debug.WriteLine("ImageServer.CreateImageAsync: First image, setting gallery thumbnail");
                    await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
                }

                // have the remaining image files generaged asyncronously so we can return asap
                Task.Run(() => GenerateRemainingFilesAndUpdateImageAsync(image, imageStream, blobServiceClient)).Forget();
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
            Debug.WriteLine($"ImageServer.UpdateImageAsync: Request charge: {response.RequestCharge}");
        }

        public async Task<List<Image>> GetGalleryImagesAsync(string galleryId)
        {
            const string query = "SELECT * FROM c WHERE c.GalleryId = @galleryId";
            var queryDefinition = new QueryDefinition(query);
            queryDefinition.WithParameter("@galleryId", galleryId);

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = container.GetItemQueryIterator<Image>(queryDefinition);
            double charge = 0;
            var images = new List<Image>();

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                images.AddRange(results);
                charge += results.RequestCharge;
            }

            Debug.WriteLine($"ImageServer.GetGalleryImagesAsync: Found {images.Count} gallery images");
            Debug.WriteLine($"ImageServer.GetGalleryImagesAsync: Total request charge: {charge}");

            return images;
        }

        public async Task<Image> GetImageAsync(string galleryId, string imageId)
        {
            if (string.IsNullOrEmpty(galleryId) || string.IsNullOrEmpty(imageId))
            {
                Debug.WriteLine("ImageServer:GetImageAsync: some args were null, returning null");
                return null;
            }

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var response = await container.ReadItemAsync<Image>(imageId, new PartitionKey(galleryId));
            Debug.WriteLine($"ImageServer:GetImageAsync: Request charge: {response.RequestCharge}");
            return response.Resource;
        }

        /// <summary>
        /// Returns a page of images with a specific tag
        /// </summary>
        /// <param name="tag">The tag used to find images for.</param>
        /// <param name="page">The page of galleries to return results from, for the first page use 1.</param>
        /// <param name="pageSize">The maximum number of galleries to return per page, i.e. 20.</param>
        /// <param name="maxResults">The maximum number of galleries to get paged results for, i.e. how many pages to look for.</param>
        public async Task<PagedResultSet<Image>> GetImagesAsync(string tag, int page = 1, int pageSize = 20, int maxResults = 500)
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
            var queryDefinition = new QueryDefinition("SELECT TOP @maxResults i.id, i.GalleryId FROM i WHERE ARRAY_CONTAINS(i.Tags, @tag) ORDER BY i.Created DESC")
                    .WithParameter("@maxResults", maxResults)
                    .WithParameter("@tag", tag);
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = container.GetItemQueryIterator<JObject>(queryDefinition);
            var ids = new List<DatabaseId>();
            double charge = 0;

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                ids.AddRange(results.Select(result => new DatabaseId { Id = result["id"].Value<string>(), PartitionKey = result["GalleryId"].Value<string>() }));
                charge += results.RequestCharge;
            }

            Debug.WriteLine($"ImageServer.GetImagesAsync(tag): Found {ids.Count} ids using query: {queryDefinition.QueryText}");
            Debug.WriteLine($"ImageServer.GetImagesAsync(tag): Total request charge: {charge}");

            // now with all the ids we know how many total results there are and so can populate paging info
            var pagedResultSet = new PagedResultSet<Image> { PageSize = pageSize, TotalResults = ids.Count, CurrentPage = page };

            // don't let users try and request a page that doesn't exist
            if (page > pagedResultSet.TotalPages)
                page = pagedResultSet.TotalPages;

            if (ids.Count > 0)
            {
                // now just retrieve a page's worth of images from the results
                var offset = (page - 1) * pageSize;
                var itemsToGet = ids.Count >= pageSize ? pageSize : ids.Count;

                // if we're on the last page just get the remaining items
                if (page == pagedResultSet.TotalPages)
                    itemsToGet = pagedResultSet.TotalResults - offset;

                var pageIds = ids.GetRange(offset, itemsToGet);

                foreach (var id in pageIds)
                    pagedResultSet.Results.Add(await GetImageAsync(id.PartitionKey, id.Id));
            }

            return pagedResultSet;
        }

        /// <summary>
        /// Causes an Image to be permanently deleted from storage and database. Will also result in some images being re-ordered to avoid holes in positions.
        /// </summary>
        public async Task DeleteImageAsync(Image image, bool performReordering = true)
        {
            // delete all image files
            // todo: optimise this by running in parallel
            var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
            await DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.SpecOriginal), blobServiceClient);
            await DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.Spec3840), blobServiceClient);
            await DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.Spec2560), blobServiceClient);
            await DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.Spec1920), blobServiceClient);
            await DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.Spec800), blobServiceClient);
            await DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.SpecLowRes), blobServiceClient);

            // make note of the image position and gallery id as we might have to re-order photos
            var position = image.Position;
            var galleryId = image.GalleryId;

            // finally, delete the database record
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var response = await container.DeleteItemAsync<Image>(image.Id, new PartitionKey(image.GalleryId));
            Debug.WriteLine($"ImageServer:DeleteImageAsync: Request charge: {response.RequestCharge}");

            // if necessary, re-order photos down-position from where the deleted photo used to be
            if (position.HasValue && performReordering)
            {
                var originalPosition = position.Value;
                Debug.WriteLine("ImageServer:DeleteImageAsync: Image had an order, re-ordering subsequent images...");

                // get the ids of images that have a position down from where our deleted image used to be
                var queryDefinition = new QueryDefinition("SELECT c.id AS Id, c.GalleryId AS PartitionKey FROM c WHERE c.GalleryId = @galleryId AND c.Position > @position ORDER BY c.Position")
                    .WithParameter("@galleryId", galleryId)
                    .WithParameter("@position", position.Value);

                var ids = await Utilities.GetIdsByQueryAsync(Constants.GalleriesContainerName, queryDefinition);
                Image newThumbnailImage = null;

                for (var index = 0; index < ids.Count; index++)
                {
                    var id = ids[index].Id;
                    var affectedImage = await GetImageAsync(galleryId, id);

                    if (index == 0 && originalPosition == 0)
                        newThumbnailImage = affectedImage;

                    // this check shouldn't be required as if one image has a position then all should
                    // but life experience suggests it's best to be sure.
                    if (affectedImage.Position == null)
                        continue;

                    affectedImage.Position = position;
                    await UpdateImageAsync(affectedImage);
                    position += 1;
                }

                // update the gallery thumbnail if we deleted the first image
                if (originalPosition == 0 && newThumbnailImage != null)
                {
                    Debug.WriteLine("ImageServer.DeleteImageAsync: New thumbnail image detected, updating gallery...");
                    var gallery = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, image.GalleryId);
                    gallery.ThumbnailStorageId = newThumbnailImage.Files.Spec800Id;
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

            // if images have not been ordered before, they will have no position set and the client
            // will be ordering images by when the images were created, so the first thing we need to do
            // is order the images like that, then re-order the images to what the user wishes.

            var hadToPerformInitialOrdering = false;
            if (!images.Any(i => i.Position.HasValue))
            {
                images = images.OrderBy(i => i.Created).ToList();
                for (var i = 0; i < images.Count; i++)
                    images[i].Position = i;

                hadToPerformInitialOrdering = true;
            }

            // now we can re-order the images
            // cut out the image being ordered first then bump up a position each image from where our image will go
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
                gallery.ThumbnailStorageId = imageBeingOrdered.Files.Spec800Id;
                await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
            }
        }

        /// <summary>
        /// Checks to see if any images in a gallery need a position setting, i.e. to accomodate new uploads and then sets the necessary positions.
        /// </summary>
        /// <param name="galleryId">The id of the gallery to check the positions on.</param>
        public async Task UpdateImagePositionsAsync(string galleryId)
        {
            var images = await GetGalleryImagesAsync(galleryId);
            if (images.Any(i => i.Position.HasValue))
            {
                // some images have been ordered, order the rest
                var orderedImages = images.Where(i => i.Position.HasValue).OrderBy(i => i.Position.Value);
                var unorderedImages = images.Where(i => i.Position.HasValue == false).OrderBy(i => i.Created);

                // ReSharper disable once PossibleInvalidOperationException - cannot contain images without positions
                var newPosition = orderedImages.Max(i => i.Position.Value) + 1;
                foreach (var image in unorderedImages)
                {
                    image.Position = newPosition;
                    newPosition += 1;

                    await UpdateImageAsync(image);
                }
            }
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
        #endregion

        #region admin methods
        /// <summary>
        /// If we add new types of generated images to the app then new image files will need generating, this method will do that.
        /// </summary>
        /// <param name="categoryId">The unique identifier for the category the gallery resides in that we want to generate missing images for.</param>
        /// <param name="galleryId">The unique identifier for the gallery to generate missing image files for.</param>
        /// <returns>A set of responses for each image generated.</returns>
        public async Task<List<string>> GenerateMissingImagesAsync(string categoryId, string galleryId)
        {
            if (string.IsNullOrEmpty(galleryId))
                throw new ArgumentException("galleryId is invalid", nameof(galleryId));

            var images = await GetGalleryImagesAsync(galleryId);
            var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
            var originalContainerClient = blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
            var responses = new List<string>();

            Parallel.ForEach(images, image =>
            {
                HandlePhotoMissingImagesGenerationAsync(image, blobServiceClient, originalContainerClient, responses).GetAwaiter().GetResult();
            });

            // update the gallery with the new thumbnail image (Spec800)
            // todo: this will not be adequate for very high pixel density displays.
            // need to expand the concept so it allows client to make best decision (maybe just put the whole Files object into Gallery).
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            if (!gallery.ThumbnailStorageId.EndsWith(".webp"))
            {
                gallery.ThumbnailStorageId = GetGalleryThumbnailImage(images).Files.Spec800Id;
                await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
                Debug.WriteLine("ImageServer:GenerateMissingImagesAsync: updated thumbnail: " + gallery.ThumbnailStorageId);
            }

            return responses;
        }

        /// <summary>
        /// Deletes all pre-generated image files and updates the image object.
        /// </summary>
        public async Task DeletePreGenImageFilesAsync(string galleryId)
        {
            var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
            var images = await GetGalleryImagesAsync(galleryId);
            Parallel.ForEach(images, image =>
            {
                DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.Spec3840), blobServiceClient).GetAwaiter().GetResult();
                DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.Spec2560), blobServiceClient).GetAwaiter().GetResult();
                DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.Spec1920), blobServiceClient).GetAwaiter().GetResult();
                DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.Spec800), blobServiceClient).GetAwaiter().GetResult();
                DeleteImageFileAsync(image, Server.Instance.GetImageFileSpec(FileSpec.SpecLowRes), blobServiceClient).GetAwaiter().GetResult();

                image.Files.Spec3840Id = null;
                image.Files.Spec2560Id = null;
                image.Files.Spec1920Id = null;
                image.Files.Spec800Id = null;
                image.Files.SpecLowResId = null;

                UpdateImageAsync(image).GetAwaiter().GetResult();
            });
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
            Debug.WriteLine($"ImageServer.GetImagesScalarByQueryAsync: Request charge: {resultSet.RequestCharge}");

            return Convert.ToInt32(resultSet.Resource.First());
        }
        #endregion

        #region private methods
        private static async Task<List<Image>> GetImagesByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = container.GetItemQueryIterator<Image>(queryDefinition);
            var users = new List<Image>();
            double charge = 0;

            while (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                charge += resultSet.RequestCharge;
                users.AddRange(resultSet);
            }

            Debug.WriteLine($"ImageServer.GetImagesByQueryAsync: Query: {queryDefinition.QueryText}");
            Debug.WriteLine($"ImageServer.GetImagesByQueryAsync: Total request charge: {charge}");

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
            Debug.WriteLine($"ImageServer.GetImageIdByQueryAsync: Request charge: {resultSet.RequestCharge}");

            return (string)resultSet.Resource.FirstOrDefault();
        }

        private static Image GetGalleryThumbnailImage(IReadOnlyCollection<Image> images)
        {
            if (images.Any(i => i.Position.HasValue))
                return images.Single(i => i.Position != null && i.Position.Value == 0);

            return images.OrderBy(i => i.Created).First();
        }

        /// <summary>
        /// Handles deleting a specific version of an image file according to file spec.
        /// </summary>
        private async Task DeleteImageFileAsync(Image image, ImageFileSpec imageFileSpec, BlobServiceClient client)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            var storageId = string.Empty;
            switch (imageFileSpec.FileSpec)
            {
                case FileSpec.SpecOriginal:
                    storageId = image.Files.OriginalId;
                    break;
                case FileSpec.Spec3840:
                    storageId = image.Files.Spec3840Id;
                    break;
                case FileSpec.Spec2560:
                    storageId = image.Files.Spec2560Id;
                    break;
                case FileSpec.Spec1920:
                    storageId = image.Files.Spec1920Id;
                    break;
                case FileSpec.Spec800:
                    storageId = image.Files.Spec800Id;
                    break;
                case FileSpec.SpecLowRes:
                    storageId = image.Files.SpecLowResId;
                    break;
            }

            if (!string.IsNullOrEmpty(storageId))
            {
                var container = client.GetBlobContainerClient(imageFileSpec.ContainerName);
                var response = await container.DeleteBlobIfExistsAsync(storageId);
                Debug.WriteLine("ImageServer.DeleteImageFileAsync: response status: " + response.Value);
                return;
            }

            Debug.WriteLine("ImageServer.DeleteImageFileAsync: storage id is null. FileSpec: " + imageFileSpec.FileSpec);
        }

        private async Task HandlePhotoMissingImagesGenerationAsync(Image image, BlobServiceClient blobServiceClient, BlobContainerClient originalContainerClient, ICollection<string> responses)
        {
            // do we have any work to do?
            if (string.IsNullOrEmpty(image.Files.OriginalId) ||
                string.IsNullOrEmpty(image.Files.Spec3840Id) ||
                string.IsNullOrEmpty(image.Files.Spec2560Id) ||
                string.IsNullOrEmpty(image.Files.Spec1920Id) ||
                string.IsNullOrEmpty(image.Files.Spec800Id) ||
                string.IsNullOrEmpty(image.Files.SpecLowResId))
            {
                // migrate the original storage id
                if (!string.IsNullOrEmpty(image.StorageId) && string.IsNullOrEmpty(image.Files.OriginalId))
                    image.Files.OriginalId = image.StorageId;

                // we do - download the source image to use for image resizing
                var blobClient = originalContainerClient.GetBlobClient(image.Files.OriginalId);
                var blob = await blobClient.DownloadAsync();

                // copy the blob stream to a new stream because the blob stream is some weird type that we can't work with.
                // we need a stream to turn into a Bitmap to get dimensions from.
                await using var ms = new MemoryStream();
                await using var originalImageStream = blob.Value.Content;
                blob.Value.Content.CopyTo(ms);
                var imageBytes = ms.ToArray();

                // are we missing dimensions metadata?
                if (!image.Metadata.Width.HasValue || !image.Metadata.Height.HasValue)
                {
                    using var bm = new Bitmap(ms);
                    image.Metadata.Width = bm.Width;
                    image.Metadata.Height = bm.Height;
                    Debug.WriteLine($"ImageServer:GenerateRemainingFilesAndUpdateImageAsync: Setting dimensions: {image.Metadata.Width} x {image.Metadata.Height}");
                }

                // execute the image generation tasks in parallel to make the most of server compute/networking resources
                var specs = new List<FileSpec> { FileSpec.Spec3840, FileSpec.Spec2560, FileSpec.Spec1920, FileSpec.Spec800, FileSpec.SpecLowRes };
                var imageUpdateNeeded = false;
                Parallel.ForEach(specs, spec => {
                    var imageGenerated = GenerateAndStoreImageFileAsync(image, spec, imageBytes, blobServiceClient).GetAwaiter().GetResult();
                    if (imageGenerated)
                    {
                        imageUpdateNeeded = true;
                        responses.Add($"Generated {spec} for image: {image.Id}");
                    }
                    else
                        responses.Add($"No {spec} needed for image: {image.Id}");
                });

                // update the image with the new image storage ids
                if (imageUpdateNeeded)
                    await UpdateImageAsync(image);
            }
            else
            {
                responses.Add("No missing files for image: " + image.Id);
            }
        }

        /// <summary>
        /// Generates an resized image of the original in WebP format (quicker, smaller files).
        /// </summary>
        /// <param name="originalImage">The byte array for the original image.</param>
        /// <param name="imageFileSpec">The name of the image file specification to base the new image.</param>
        /// <returns>A new image stream for the resized image</returns>
        private static async Task<Stream> GenerateImageAsync(byte[] originalImage, ImageFileSpec imageFileSpec)
        {
            var timer = new Stopwatch();
            timer.Start();

            using var job = new ImageJob();
            var result = await job.Decode(originalImage)
                //.ConstrainWithin((uint?)imageFileSpec.PixelLength, (uint?)imageFileSpec.PixelLength, new ResampleHints().SetSharpen(41.0f, SharpenWhen.Always).SetResampleFilters(InterpolationFilter.Robidoux, InterpolationFilter.Cubic))
                .ConstrainWithin((uint?)imageFileSpec.PixelLength, (uint?)imageFileSpec.PixelLength, new ResampleHints().SetSharpen(35.0f, SharpenWhen.Downscaling).SetResampleFilters(InterpolationFilter.Robidoux, null))
                .EncodeToBytes(new WebPLossyEncoder(imageFileSpec.Quality))
                .Finish()
                .SetSecurityOptions(new SecurityOptions()
                    .SetMaxDecodeSize(new FrameSizeLimit(12000, 12000, 100))
                    .SetMaxFrameSize(new FrameSizeLimit(12000, 12000, 100))
                    .SetMaxEncodeSize(new FrameSizeLimit(12000, 12000, 30)))
                .InProcessAsync();

            var newImageBytes = result.First.TryGetBytes();
            if (newImageBytes.HasValue)
            {
                var newStream = new MemoryStream(newImageBytes.Value.ToArray());

                timer.Stop();
                Debug.WriteLine($"ImageServer.GenerateImageAsync: Done. {imageFileSpec.FileSpec}. Elapsed time: {timer.Elapsed}");
                return newStream;
            }

            timer.Stop();
            Debug.WriteLine("ImageServer.GenerateImageAsync: Couldn't generate new image! Elapsed time: " + timer.Elapsed);
            return null;
        }

        /// <summary>
        /// Generates all remaining (i.e. all but the Spec800) image files, stores them in Azure Blob storage and updates the image object.
        /// </summary>
        private async Task GenerateRemainingFilesAndUpdateImageAsync(Image image, Stream imageStream, BlobServiceClient blobServiceClient)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

            if (blobServiceClient == null)
                throw new ArgumentNullException(nameof(blobServiceClient));

            // generate and store each image in parallel
            var specs = new List<FileSpec> { FileSpec.Spec3840, FileSpec.Spec2560, FileSpec.Spec1920, FileSpec.SpecLowRes };
            Parallel.ForEach(specs, spec => {
                var imageBytes = Utilities.ConvertStreamToBytes(imageStream);
                GenerateAndStoreImageFileAsync(image, spec, imageBytes, blobServiceClient).GetAwaiter().GetResult();
            });

            // update the image with the new image file storage ids
            await UpdateImageAsync(image);

            Debug.WriteLine("ImageServer.GenerateRemainingFilesAndUpdateImageAsync: Generated all images");
        }

        /// <summary>
        /// Generates a new image file according to an image file spec, uploads it to Azure Blob storage and assigns the relevant Files property storage id value.
        /// </summary>
        private static async Task<bool> GenerateAndStoreImageFileAsync(Image image, FileSpec fileSpec, byte[] imageBytes, BlobServiceClient blobServiceClient)
        {
            // TEMPORARY: force re-generations whilst we decide on a encoding
            // is there an image missing?
            var workToDo = fileSpec switch
            {
                FileSpec.Spec3840 when !string.IsNullOrEmpty(image.Files.Spec3840Id) => false,
                FileSpec.Spec2560 when !string.IsNullOrEmpty(image.Files.Spec2560Id) => false,
                FileSpec.Spec1920 when !string.IsNullOrEmpty(image.Files.Spec1920Id) => false,
                FileSpec.Spec800 when !string.IsNullOrEmpty(image.Files.Spec800Id) => false,
                FileSpec.SpecLowRes when !string.IsNullOrEmpty(image.Files.SpecLowResId) => false,
                _ => true
            };

            if (!workToDo)
            {
                Debug.WriteLine($"ImageSever:GenerateAndStoreImageFileAsync: {fileSpec} has already been generated, no work to do");
                return false;
            }

            var imageFileSpec = Server.Instance.GetImageFileSpec(fileSpec);

            // we only generate images if the source image is larger than the size we're being asked to resize to, i.e. we only go down in size
            var longestSide = image.Metadata.Width > image.Metadata.Height ? image.Metadata.Width : image.Metadata.Height;
            if (longestSide <= imageFileSpec.PixelLength)
            {
                Debug.WriteLine($"ImageServer.GenerateAndStoreImageFileAsync: image too small for this file spec: {fileSpec}: {image.Metadata.Width} x {image.Metadata.Height}");
                return false;
            }

            // create the new image file
            var storageId = imageFileSpec.GetStorageId(image);
            await using (var imageFile = await GenerateImageAsync(imageBytes, imageFileSpec))
            {
                // upload the new image file to storage (delete any old version first)
                var containerClient = blobServiceClient.GetBlobContainerClient(imageFileSpec.ContainerName);
                await containerClient.DeleteBlobIfExistsAsync(storageId);
                await containerClient.UploadBlobAsync(storageId, imageFile);
            }

            // now update the image with the new file storage id
            switch (fileSpec)
            {
                case FileSpec.Spec3840:
                    image.Files.Spec3840Id = storageId;
                    break;
                case FileSpec.Spec2560:
                    image.Files.Spec2560Id = storageId;
                    break;
                case FileSpec.Spec1920:
                    image.Files.Spec1920Id = storageId;
                    break;
                case FileSpec.Spec800:
                    image.Files.Spec800Id = storageId;
                    break;
                case FileSpec.SpecLowRes:
                    image.Files.SpecLowResId = storageId;
                    break;
            }

            return true;
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
        private static void ParseAndAssignImageMetadata(Image image, Stream imageStream)
        {
            // whilst image dimensions can be extracted from metadata in some cases, not in every case and this isn't acceptable
            using var bm = new Bitmap(imageStream);
            image.Metadata.Width = bm.Width;
            image.Metadata.Height = bm.Height;

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

            var credit = GetImageCredit(directories);
            if (credit.HasValue())
                image.Credit = credit;

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

                var imageDescription = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagImageDescription);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (imageDescription != null && imageDescription.Description.HasValue())
                    image.Caption = imageDescription.Description;
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
                var objectName = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagObjectName);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (objectName != null && objectName.Description.HasValue())
                    image.Name = objectName.Description;

                var keywords = iptcDirectory.GetKeywords();
                if (keywords != null)
                {
                    foreach (var keyword in keywords)
                    {
                        // make sure we don't add duplicate keywords
                        if (keyword.HasValue() && !image.Tags.Any(t => t.Equals(keyword, StringComparison.CurrentCultureIgnoreCase)))
                            image.Tags.Add(keyword.ToLower());
                    }
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

                var isoTagProcessed = isoTag.Description;
                if (isoTagProcessed.StartsWith("ISO "))
                    isoTagProcessed = isoTag.Description.Split(' ')[1];

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
        public static string GetImageCredit(IEnumerable<Directory> directories)
        {
            var enumerable = directories as Directory[] ?? directories.ToArray();
            var exifIfd0Directory = enumerable.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (exifIfd0Directory != null)
            {
                var creditTag = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagCopyright);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (creditTag != null && creditTag.Description.HasValue())
                    return creditTag.Description;
            }

            var iptcDirectory = enumerable.OfType<IptcDirectory>().FirstOrDefault();
            if (iptcDirectory != null)
            {
                var creditTag = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagCredit);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (creditTag != null && creditTag.Description.HasValue())
                    return creditTag.Description;
            }

            return null;
        }
        #endregion
    }
}
