using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models.Utilities;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.TagsMigrator
{
    internal class Program
    {
        #region members
        private static IConfiguration _configuration;
        #endregion

        private static async Task Main(string[] args)
        {
            Console.WriteLine("Starting tags migrator...");

            if (args == null || args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No configuration filename argument supplied. Cannot continue.");
                return;
            }

            // setup configuration
            _configuration = new ConfigurationBuilder()
                .AddJsonFile(args[0], optional: false, reloadOnChange: true)
                .Build();

            // initialise Server
            await Server.Instance.SetConfigurationAsync(_configuration);

            // get a list of image ids that haven't had their tags migrated yet
            var queryDefinition = new QueryDefinition("SELECT i.id AS Id, i.GalleryId AS PartitionKey FROM i WHERE IS_DEFINED(i.TagsCsv) = false");
            var ids = await Server.GetIdsByQueryAsync(Constants.ImagesContainerName, queryDefinition);

            foreach (var id in ids)
            {
                var i = await Server.Instance.Images.GetImageAsync(id.PartitionKey, id.Id);
                i.TagsCsv = string.Join(',', i.Tags);
                i.Tags = null;
                await Server.Instance.Images.UpdateImageAsync(i);
                Console.WriteLine($"{i.Id} - {i.GalleryId}: Tags migrated: {i.TagsCsv}");
            }
        }
    }
}
