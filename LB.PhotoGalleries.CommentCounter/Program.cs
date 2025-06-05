using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.CommentCounter.Models;
using LB.PhotoGalleries.Models.Utilities;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.CommentCounter
{
    internal class Program
    {
        private static IConfiguration _configuration;
        private static CosmosClient _cosmosClient;
        private static Database _database;
        private static Container _imagesContainer;
        private static Container _galleriesContainer;

        private static async Task Main(string[] args)
        {
            // go over all galleries and images with comments and update the gallery comment count
            // used as a one-off to set the initial values for this new property, though could potentially be
            // modified to re-calculate all counts if need be.

            // oct '21: modified to fill in null values
            
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
            _galleriesContainer = _database.GetContainer(Constants.GalleriesContainerName);

            // query the database for galleries without CommentCount, set their values to zero
            const string galleryQuery = "SELECT g.id, g.CategoryId FROM Galleries g WHERE NOT IS_DEFINED(g.CommentCount)";
            var galleryQueryDefinition = new QueryDefinition(galleryQuery);
            Console.WriteLine("Getting gallery stubs...");
            var galleryQueryResult = _galleriesContainer.GetItemQueryIterator<GalleryCommentCountStub>(galleryQueryDefinition);
            Console.WriteLine("Got gallery stubs. Enumerating...");

            while (galleryQueryResult.HasMoreResults)
            {
                var galleryCommentCountStubs = await galleryQueryResult.ReadNextAsync();
                foreach (var galleryCommentCountStub in galleryCommentCountStubs)
                {
                    var gallery = await Server.Instance.Galleries.GetGalleryAsync(galleryCommentCountStub.CategoryId, galleryCommentCountStub.Id);
                    gallery.CommentCount = 0;
                    await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
                    Console.WriteLine("Gallery comments set to zero for gallery id: " + galleryCommentCountStub.Id);
                }
            }

            // query the database for images without CommentCount values, then set them to zero
            const string imageQuery = "SELECT i.id, i.GalleryId FROM Images i WHERE NOT IS_DEFINED(i.CommentCount)";
            var imageQueryDefinition = new QueryDefinition(imageQuery);
            Console.WriteLine("Getting image stubs...");
            var imageQueryResult = _imagesContainer.GetItemQueryIterator<ImageCommentCountStub>(imageQueryDefinition);
            Console.WriteLine("Got image stubs. Enumerating...");

            while (imageQueryResult.HasMoreResults)
            {
                var imageCommentCountStubs = await imageQueryResult.ReadNextAsync();
                foreach (var imageCommentCountStub in imageCommentCountStubs)
                {
                    var image = await Server.Instance.Images.GetImageAsync(imageCommentCountStub.GalleryId, imageCommentCountStub.Id);
                    image.CommentCount = 0;
                    await Server.Instance.Images.UpdateImageAsync(image);
                    Console.WriteLine($"Set image CommentCount to zero for image id: {image.Id}");
                }
            }
        }
    }
}
