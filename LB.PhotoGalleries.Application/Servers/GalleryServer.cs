using LB.PhotoGalleries.Application.Models;
using Microsoft.Azure.Cosmos;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
        public async Task<Gallery> GetGalleryAsync(string galleryId)
        {
            const string query = "SELECT * FROM c WHERE c.Id = '@galleryId'";
            var queryDefinition = new QueryDefinition(query);
            queryDefinition.WithParameter("@galleryId", galleryId);
            return await GetGalleryByQueryAsync(queryDefinition);
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
            const string query = "SELECT * FROM c WHERE c.LegacyGuidId = '@galleryLegacyId'";
            var queryDefinition = new QueryDefinition(query);
            queryDefinition.WithParameter("@galleryLegacyId", galleryLegacyId);
            return await GetGalleryByQueryAsync(queryDefinition);
        }

        public async Task CreateOrUpdateGalleryAsync(Gallery gallery)
        {
            if (gallery == null)
                throw new InvalidOperationException("Gallery is null");

            if (!gallery.IsValid())
                throw new InvalidOperationException("Gallery is not valid. Check that all required properties are set");

            var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
            var partitionKey = new PartitionKey(gallery.CategoryId);
            var response = await container.UpsertItemAsync(gallery, partitionKey);
            var createdItem = response.StatusCode == HttpStatusCode.Created;
            Debug.WriteLine("GalleryServer.CreateOrUpdateGalleryAsync: Created gallery? " + createdItem);
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
                    Debug.WriteLine("GalleryServer.GetGalleryAsync(): Found a gallery using query: " + queryDefinition.QueryText);
                    return gallery;
                }
            }

            throw new InvalidOperationException("No gallery found using query: " + queryDefinition.QueryText);
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
        #endregion
    }
}
