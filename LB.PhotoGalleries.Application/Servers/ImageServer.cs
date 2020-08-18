using Azure.Storage.Blobs;
using LB.PhotoGalleries.Application.Models;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                var image = new Image
                {
                    Id = Guid.NewGuid() + Path.GetExtension(filename).ToLower(),
                    Name = Path.GetFileNameWithoutExtension(filename),
                    GalleryId = galleryId
                };

                if (!image.IsValid())
                    throw new InvalidOperationException("Image would be invalid. PLease check all required properties are set.");

                // upload the file to storage
                var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
                await blobContainerClient.UploadBlobAsync(image.Id, imageStream);
                imageStream.Close();

                // create the database record
                // note: we're not setting position on each image at this point as it's expensive to do in terms of request units and might not ever need to be done
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
        #endregion

        #region internal methods
        internal async Task<List<Image>> GetImagesUserHasCommentedOnAsync(string userId)
        {
            const string query = "SELECT * FROM c WHERE c.Comments.CreatedByUserId = @userId";
            var queryDefinition = new QueryDefinition(query).WithParameter("@userId", userId);
            return await GetImagesByQueryAsync(queryDefinition);
        }

        internal async Task<int> GetImagesScalarByQueryAsync(QueryDefinition queryDefinition, string queryColumnName)
        {
            if (queryDefinition == null)
                throw new InvalidOperationException("queryDefinition is null");

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var result = container.GetItemQueryIterator<object>(queryDefinition);

            var count = 0;
            if (!result.HasMoreResults)
                return count;

            var resultSet = await result.ReadNextAsync();
            Debug.WriteLine($"ImageServer.GetImagesScalarByQueryAsync: Query: {queryDefinition.QueryText}");
            Debug.WriteLine($"ImageServer.GetImagesScalarByQueryAsync: Request charge: {resultSet.RequestCharge}");
            foreach (JObject item in resultSet)
                count = (int)item[queryColumnName];

            return count;
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
