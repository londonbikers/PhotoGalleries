using Azure.Storage.Blobs;
using LB.PhotoGalleries.Application.Models;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        /// <param name="galleryId">The gallery this image is going to be contained within.</param>
        /// <param name="imageStream">The stream for the uploaded image file.</param>
        /// <param name="filename">The original filename provided by the client.</param>
        public async Task CreateImageAsync(string galleryId, Stream imageStream, string filename)
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
                // note: we don't set a position as there's no easy way to do this at this point. instead
                // we let the clients order by created date initially and then when/if a photographer orders the photos
                // then the position attribute is used to order images.
                var id = Guid.NewGuid().ToString();
                var image = new Image
                {
                    Id = id,
                    StorageId = id + Path.GetExtension(filename).ToLower(),
                    Name = Path.GetFileNameWithoutExtension(filename),
                    GalleryId = galleryId
                };

                if (!image.IsValid())
                    throw new InvalidOperationException("Image would be invalid. PLease check all required properties are set.");

                // upload the file to storage
                var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
                await blobContainerClient.UploadBlobAsync(image.StorageId, imageStream);
                imageStream.Close();

                // create the database record
                var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
                var response = await container.CreateItemAsync(image, new PartitionKey(image.GalleryId));
                Debug.WriteLine($"ImageServer.CreateImageAsync: Request charge: {response.RequestCharge}");
            }
            catch
            {
                // make sure we release valuable server resources in the event of a problem creating the image
                imageStream?.Close();
                throw;
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
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var response = await container.ReadItemAsync<Image>(imageId, new PartitionKey(galleryId));
            Debug.WriteLine($"ImageServer:GetImageAsync: Request charge: {response.RequestCharge}");
            return response.Resource;
        }

        public async Task DeleteImageAsync(Image image)
        {
            // delete the original image from storage (any other resized caches will auto-expire and be deleted)
            var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
            var blobResponse = await blobContainerClient.DeleteBlobAsync(image.StorageId);
            Debug.WriteLine($"ImageServer:DeleteImageAsync: Blob delete response: {blobResponse.Status} - {blobResponse.ReasonPhrase}");

            // finally, delete the database record
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var response = await container.DeleteItemAsync<Image>(image.Id, new PartitionKey(image.GalleryId));
            Debug.WriteLine($"ImageServer:DeleteImageAsync: Request charge: {response.RequestCharge}");
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
                newPosition = newPosition + 1;
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
        #endregion
    }
}
