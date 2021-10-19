using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.ImageCommentCounter.Models;
using LB.PhotoGalleries.Models.Utilities;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.ImageCommentCounter
{
    internal class Program
    {
        private static IConfiguration _configuration;
        private static CosmosClient _cosmosClient;
        private static Database _database;
        private static Container _imagesContainer;

        private static async Task Main(string[] args)
        {
            // fixes any incorrect CommentCount values on images. some mismatches have been detected.
            _configuration = new ConfigurationBuilder()
                .AddJsonFile(args[0], optional: false)
                .Build();

            // initialise the Application Server
            await Server.Instance.SetConfigurationAsync(_configuration);

            // authenticate with the CosmosDB service and create a client we can re-use
            _cosmosClient = new CosmosClient(_configuration["CosmosDB:Uri"], _configuration["CosmosDB:PrimaryKey"]);

            // create the CosmosDB database if it doesn't already exist
            var response = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_configuration["CosmosDB:DatabaseName"], ThroughputProperties.CreateManualThroughput(400));
            
            // keep a reference to the database so other parts of the app can easily interact with it
            _database = response.Database;
            _imagesContainer = _database.GetContainer(Constants.ImagesContainerName);

            // query the database for images without CommentCount values, check if they actually do have comments and update the CommentCount if necessary
            const string imageQuery = "SELECT i.id, i.GalleryId, i.Comments FROM Images i WHERE i.CommentCount = 0";
            var imageQueryDefinition = new QueryDefinition(imageQuery);
            Console.WriteLine("Getting image stubs...");
            var imageQueryResult = _imagesContainer.GetItemQueryIterator<ImageStub>(imageQueryDefinition);
            Console.WriteLine("Got image stubs. Enumerating...");

            var commentCountsOkay = 0;
            while (imageQueryResult.HasMoreResults)
            {
                var imageStubs = await imageQueryResult.ReadNextAsync();
                foreach (var imageStub in imageStubs)
                {
                    if (imageStub.Comments.Count > 0)
                    {
                        var image = await Server.Instance.Images.GetImageAsync(imageStub.GalleryId, imageStub.Id);
                        image.CommentCount = imageStub.Comments.Count;
                        await Server.Instance.Images.UpdateImageAsync(image);
                        Console.WriteLine($"Set image CommentCount to {image.CommentCount} for image id: {image.Id}");
                    }
                    else
                    {
                        commentCountsOkay++;
                    }
                }
            }

            Console.WriteLine("---------------------");
            Console.WriteLine($"{commentCountsOkay} image CommentCounts were accurate.");
        }
    }
}
