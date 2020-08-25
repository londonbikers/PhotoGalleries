using LB.PhotoGalleries.Application.Models;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Application
{
    internal class Utilities
    {
        /// <summary>
        /// Gets a pairing of partition key and id via a supplied query.
        /// The query must return the partition key as 'PartitionKey' and the id as 'Id'.
        /// </summary>
        internal async Task<List<DatabaseId>> GetIdsByQueryAsync(string containerName, QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(containerName);
            var queryResult = container.GetItemQueryIterator<JObject>(queryDefinition);
            var ids = new List<DatabaseId>();
            double charge = 0;

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                ids.AddRange(results.Select(result => new DatabaseId { Id = result["Id"].Value<string>(), PartitionKey = result["PartitionKey"].Value<string>() }));
                charge += results.RequestCharge;
            }

            Debug.WriteLine($"Utilities.GetIdsByQueryAsync: Found {ids.Count} DatabaseIds using query: {queryDefinition.QueryText}");
            Debug.WriteLine($"Utilities.GetIdsByQueryAsync: Total request charge: {charge}");

            return ids;
        }
    }
}
