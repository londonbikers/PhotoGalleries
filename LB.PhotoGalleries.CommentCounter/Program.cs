using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.CommentCounter.Models;
using LB.PhotoGalleries.Models.Utilities;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
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

            // query the database for galleries with comments, update count
            const string galleryQuery = "SELECT g.id, g.CategoryId, ARRAY_LENGTH(g.Comments) AS CommentCount FROM Galleries g WHERE ARRAY_LENGTH(g.Comments) > 0 AND (IS_DEFINED(g.CommentCount) = false OR g.CommentCount = 0)";
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
                    gallery.CommentCount = galleryCommentCountStub.CommentCount;
                    await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
                    Console.WriteLine("Gallery comments set for gallery id: " + galleryCommentCountStub.Id);
                }
            }

            // query the database for images with comments, keep track of galleries and comment counts, then update gallery counts
            var galleryStubs = new List<GalleryCommentCountStub>();
            const string imageQuery = "SELECT TOP 10 i.id, i.GalleryCategoryId, i.GalleryId, ARRAY_LENGTH(i.Comments) AS CommentCount FROM Images i WHERE ARRAY_LENGTH(i.Comments) > 0";
            var imageQueryDefinition = new QueryDefinition(imageQuery);
            Console.WriteLine("Getting image stubs...");
            var imageQueryResult = _imagesContainer.GetItemQueryIterator<ImageCommentCountStub>(imageQueryDefinition);
            Console.WriteLine("Got image stubs. Enumerating...");

            while (imageQueryResult.HasMoreResults)
            {
                var imageCommentCountStubs = await imageQueryResult.ReadNextAsync();
                foreach (var imageCommentCountStub in imageCommentCountStubs)
                {
                    // check if we have a gallery stub already
                    // if so add the image comment count to that
                    // if not, create a stub and assign the image count
                    var galleryStub = galleryStubs.SingleOrDefault(g => g.Id == imageCommentCountStub.GalleryId);
                    if (galleryStub == null)
                    {
                        galleryStub = new GalleryCommentCountStub
                        {
                            CategoryId = imageCommentCountStub.GalleryCategoryId,
                            Id = imageCommentCountStub.GalleryId
                        };
                        galleryStubs.Add(galleryStub);
                    }
                    galleryStub.CommentCount += imageCommentCountStub.CommentCount;
                }
            }

            Console.WriteLine($"Added image comment counts to gallery stubs {galleryStubs.Count}");

            // now that we've got a list of gallery stubs with total image counts we can enumerate them and update the gallery comment counts
            foreach (var galleryStub in galleryStubs)
            {
                var gallery = await Server.Instance.Galleries.GetGalleryAsync(galleryStub.CategoryId, galleryStub.Id);
                gallery.CommentCount += galleryStub.CommentCount;
                await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
                Console.WriteLine("Gallery image comments set for gallery id: " + galleryStub.Id);
            }
        }
    }
}
