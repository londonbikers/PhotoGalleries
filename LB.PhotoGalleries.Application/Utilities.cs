using LB.PhotoGalleries.Models;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        internal static async Task<List<DatabaseId>> GetIdsByQueryAsync(string containerName, QueryDefinition queryDefinition)
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

        /// <summary>
        /// Does what it says on the tin. Converts a stream to a byte array.
        /// </summary>
        internal static byte[] ConvertStreamToBytes(Stream input)
        {
            if (input == null)
                return null;

            if (input.CanSeek && input.Position != 0)
                input.Position = 0;

            using var ms = new MemoryStream();
            input.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Generates a new unique identifier for use on objects.
        /// </summary>
        internal static string GenerateId()
        {
            return Guid.NewGuid().ToString().Replace("-", string.Empty);
        }

        /// <summary>
        /// Base64 encodes a string.
        /// </summary>
        internal static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
    }

    internal static class Extensions
    {
        /// <summary>
        /// Determines if a string has a usable value, i.e. is not null, empty or made up of just whitespace.
        /// </summary>
        internal static bool HasValue(this string str)
        {
            return !string.IsNullOrEmpty(str) && !string.IsNullOrWhiteSpace(str);
        }
    }
}
