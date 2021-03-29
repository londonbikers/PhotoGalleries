using Azure.Storage.Blobs;
using LB.PhotoGalleries.Models.Utilities;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Worker
{
    /// <summary>
    /// Background worker process for LB Photo Galleries.
    /// Processes an Azure Storage message queue to resize down new versions of uploaded image files.
    /// </summary>
    internal class Program
    {
        #region members
        private static IConfiguration _configuration;
        private static CosmosClient _cosmosClient;
        private static BlobServiceClient _blobServiceClient;
        private static Database _database;
        private static Container _imagesContainer;
        private static Container _galleriesContainer;
        private static ILogger _log;
        private static ImageHandler _imageHandler;
        #endregion

        private static async Task Main(string[] args)
        {
            Console.WriteLine("Starting worker...");

            if (args == null || args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No configuration filename argument supplied. Cannot continue.");
                return;
            }

            // setup configuration
            _configuration = new ConfigurationBuilder()
                .AddJsonFile(args[0], optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            // setup logging
            var loggerConfiguration = new LoggerConfiguration();
            var loggingMinimumLevel = _configuration["Logging:MinimumLevel"];
            switch (loggingMinimumLevel)
            {
                case "Verbose":
                    loggerConfiguration.MinimumLevel.Verbose();
                    break;
                case "Debug":
                    loggerConfiguration.MinimumLevel.Debug();
                    break;
                case "Information":
                    loggerConfiguration.MinimumLevel.Information();
                    break;
                case "Warning":
                    loggerConfiguration.MinimumLevel.Warning();
                    break;
                case "Error":
                    loggerConfiguration.MinimumLevel.Error();
                    break;
                case "Fatal":
                    loggerConfiguration.MinimumLevel.Fatal();
                    break;
            }
            loggerConfiguration.WriteTo.File(Path.Combine(_configuration["Logging:Path"], "lb.photogalleries.worker.log"), rollingInterval: RollingInterval.Day);
            _log = loggerConfiguration.CreateLogger();

            try
            {
                // setup clients/references
                _cosmosClient = new CosmosClient(_configuration["CosmosDB:Uri"], _configuration["CosmosDB:PrimaryKey"]);
                _blobServiceClient = new BlobServiceClient(_configuration["Storage:ConnectionString"]);
                _database = _cosmosClient.GetDatabase(_configuration["CosmosDB:DatabaseName"]);
                _imagesContainer = _database.GetContainer(Constants.ImagesContainerName);
                _galleriesContainer = _database.GetContainer(Constants.GalleriesContainerName);

                // setup handlers
                _imageHandler = new ImageHandler(_configuration, _log, _blobServiceClient, _galleriesContainer, _imagesContainer);
                await _imageHandler.StartAsync();

            }
            catch (Exception exception)
            {
                _log.Fatal(exception, "LB.PhotoGalleries.Worker.Program.Main() - Unhandled exception!");
            }
        }
    }
}
