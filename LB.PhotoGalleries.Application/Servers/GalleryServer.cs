using Azure.Storage.Blobs;
using LB.PhotoGalleries.Application.Models;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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

        #region gallery methods
        public async Task<Gallery> GetGalleryAsync(string categoryId, string galleryId)
        {
            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var response = await container.ReadItemAsync<Gallery>(galleryId, new PartitionKey(categoryId));
            Debug.WriteLine($"GalleryServer:GetGalleryAsync: Request charge: {response.RequestCharge}");
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

        public async Task<List<GalleryStub>> GetLatestGalleriesAsync(int maxResults)
        {
            // limit the results to avoid putting excessive strain on the database and from incurring unnecessary charges
            if (maxResults > 100)
                maxResults = 100;

            var queryDefinition = new QueryDefinition("SELECT TOP @maxResults c.id, c.CategoryId, c.Name, c.Active, c.Created FROM c ORDER BY c.Created DESC").WithParameter("@maxResults", maxResults);
            return await GetGalleryStubsByQueryAsync(queryDefinition);
        }

        /// <summary>
        /// Performs a search for galleries with a given search term in their name.
        /// </summary>
        public async Task<List<GalleryStub>> SearchForGalleriesAsync(string searchString, int maxResults)
        {
            // limit the results to avoid putting excessive strain on the database and from incurring unnecessary charges
            if (maxResults > 100)
                maxResults = 100;

            var queryDefinition =
                new QueryDefinition("SELECT TOP @maxResults c.id, c.CategoryId, c.Name, c.Active, c.Created FROM c WHERE CONTAINS(c.Name, @searchString, true) ORDER BY c.Created DESC")
                    .WithParameter("@searchString", searchString)
                    .WithParameter("@maxResults", maxResults);

            return await GetGalleryStubsByQueryAsync(queryDefinition);
        }

        public async Task CreateOrUpdateGalleryAsync(Gallery gallery)
        {
            if (gallery == null)
                throw new ArgumentNullException(nameof(gallery));

            if (!gallery.IsValid())
                throw new InvalidOperationException("Gallery is not valid. Check that all required properties are set");

            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var partitionKey = new PartitionKey(gallery.CategoryId);
            var response = await container.UpsertItemAsync(gallery, partitionKey);
            var createdItem = response.StatusCode == HttpStatusCode.Created;
            Debug.WriteLine("GalleryServer.CreateOrUpdateGalleryAsync: Created gallery? " + createdItem);
            Debug.WriteLine("GalleryServer.CreateOrUpdateGalleryAsync: Request charge: " + response.RequestCharge);
        }
        #endregion

        #region image methods
        public async Task AddImageAsync(string categoryId, string galleryId, Stream imageStream, string filename)
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
                Name = Path.GetFileNameWithoutExtension(filename)
            };

            var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);

            await blobContainerClient.UploadBlobAsync(image.Id, imageStream);
            imageStream.Close();

            // create the gallery image
            // todo: find a way to upload images without having to re-save the gallery and incur request charges each time
            var gallery = await GetGalleryAsync(categoryId, galleryId);

            var position = 0;
            if (gallery.Images.Count > 0)
                position = gallery.Images.Keys.Max() + 1;

            gallery.Images.Add(position, image);
            await CreateOrUpdateGalleryAsync(gallery);
        }
        #endregion

        #region internal methods
        internal async Task<int> GetGalleriesScalarByQueryAsync(QueryDefinition queryDefinition, string queryColumnName)
        {
            if (queryDefinition == null)
                throw new InvalidOperationException("queryDefinition is null");

            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var result = container.GetItemQueryIterator<object>(queryDefinition);

            var count = 0;
            if (!result.HasMoreResults)
                return count;

            var resultSet = await result.ReadNextAsync();
            Debug.WriteLine("UserServer.GetGalleriesScalarByQueryAsync: Query: " + queryDefinition.QueryText);
            Debug.WriteLine("UserServer.GetGalleriesScalarByQueryAsync: Request charge: " + resultSet.RequestCharge);
            foreach (JObject item in resultSet)
                count = (int)item[queryColumnName];

            return count;
        }

        internal async Task<List<GalleryStub>> GetGalleryStubsByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var queryResult = container.GetItemQueryIterator<GalleryStub>(queryDefinition);
            var galleryStubs = new List<GalleryStub>();
            double charge = 0;

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                galleryStubs.AddRange(results);
                charge += results.RequestCharge;
            }

            Debug.WriteLine($"GalleryServer.GetGalleryStubsByQueryAsync: Found {galleryStubs.Count} gallery stubs using query: {queryDefinition.QueryText}");
            Debug.WriteLine($"GalleryServer.GetGalleryStubsByQueryAsync: Total request charge: {charge}");

            return galleryStubs;
        }

        internal async Task<List<Gallery>> GetGalleriesByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var queryResult = container.GetItemQueryIterator<Gallery>(queryDefinition);
            var galleries = new List<Gallery>();
            double charge = 0;

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                galleries.AddRange(results);
                charge += results.RequestCharge;
            }

            Debug.WriteLine($"GalleryServer.GetGalleriesByQueryAsync: Found {galleries.Count} galleries using query: {queryDefinition.QueryText}");
            Debug.WriteLine($"GalleryServer.GetGalleriesByQueryAsync: Total request charge: {charge}");

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
                    Debug.WriteLine("GalleryServer.GetGalleryAsync: Request charge: " + resultSet.RequestCharge);
                    return gallery;
                }
            }

            throw new InvalidOperationException("No gallery found using query: " + queryDefinition.QueryText);
        }
        #endregion
    }
}
