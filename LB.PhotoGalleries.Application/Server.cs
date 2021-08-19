using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using LB.PhotoGalleries.Application.Servers;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Application
{
    /// <summary>
	/// The application entry point. Employs a singleton pattern.
	/// </summary>
    public class Server
    {
        #region accessors
        /// <summary>
        /// Retrieves the single instance of the application.
        /// </summary>
        public static Server Instance { get; }
        public CategoryServer Categories { get; internal set; }
        public GalleryServer Galleries { get; internal set; }
        public ImageServer Images { get; internal set; }
        public UserServer Users { get; internal set; }

        /// <summary>
        /// Provides access to application configuration data, i.e. connection strings.
        /// Needs to be set on startup by the client using SetConfiguration().
        /// </summary>
        internal IConfiguration Configuration { get; set; }
        internal CosmosClient CosmosClient { get; set; }
        internal Database Database { get; set; }
        internal BlobServiceClient BlobServiceClient { get; set; }
        internal QueueClient ImageProcessingQueueClient { get; set; }
        internal QueueClient NotificationProcessingQueueClient { get; set; }
        #endregion

        #region constructors
        static Server()
        {
            Instance = new Server
            {
                Categories = new CategoryServer(),
                Galleries = new GalleryServer(),
                Images = new ImageServer(),
                Users = new UserServer()
            };

            Log.Information("Server constructed");
        }
        #endregion

        #region public methods
        /// <summary>
        /// Must be set by the client consuming Server before the server can be used so it can obtain connection strings, etc.
        /// i.e. Set in Startup.cs for an MVC application.
        /// </summary>
        public async Task SetConfigurationAsync(IConfiguration configuration)
        {
            Log.Information("Server.SetConfigurationAsync()");
            Configuration = configuration;
            await InitialiseDatabaseAsync();
            await InitialiseStorageAsync();
            await InitialiseQueuesAsync();
        }
        #endregion

        #region internal methods
        /// <summary>
        /// Gets a pairing of partition key and id via a supplied query.
        /// The query must return the partition key as 'PartitionKey' and the id as 'Id'.
        /// </summary>
        public static async Task<List<DatabaseId>> GetIdsByQueryAsync(string containerName, QueryDefinition queryDefinition)
        {
            var container = Instance.Database.GetContainer(containerName);
            var queryResult = container.GetItemQueryIterator<JObject>(queryDefinition);
            var ids = new List<DatabaseId>();
            double charge = 0;
            TimeSpan elapsedTime = default;

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                ids.AddRange(results.Select(result => new DatabaseId(result["Id"].Value<string>(), result["PartitionKey"].Value<string>())));
                elapsedTime += results.Diagnostics.GetClientElapsedTime();
                charge += results.RequestCharge;
            }

            Log.Debug($"Utilities.GetIdsByQueryAsync: Found {ids.Count} DatabaseIds using query: {queryDefinition.QueryText}");
            Log.Debug($"Utilities.GetIdsByQueryAsync: Total request charge: {charge}. Total elapsed time: {elapsedTime.TotalMilliseconds} ms");

            return ids;
        }
        #endregion

        #region private methods
        private async Task InitialiseDatabaseAsync()
        {
            // authenticate with the CosmosDB service and create a client we can re-use
            CosmosClient = new CosmosClient(Configuration["CosmosDB:Uri"], Configuration["CosmosDB:PrimaryKey"]);

            // create the CosmosDB database if it doesn't already exist
            var response = await CosmosClient.CreateDatabaseIfNotExistsAsync(Configuration["CosmosDB:DatabaseName"], ThroughputProperties.CreateManualThroughput(400));
            var createdDatabase = response.StatusCode == HttpStatusCode.Created;
            Log.Debug("Server.InitialiseDatabaseAsync: Created database? " + createdDatabase);

            // keep a reference to the database so other parts of the app can easily interact with it
            Database = response.Database;

            // create containers for all our top level objects we want to persist
            var createdCategoriesContainerResponse = await Database.CreateContainerIfNotExistsAsync(Constants.CategoriesContainerName, "/PartitionKey");
            var createdCategoriesContainer = createdCategoriesContainerResponse.StatusCode == HttpStatusCode.Created;
            Log.Information("Server.InitialiseDatabaseAsync: Created categories container? " + createdCategoriesContainer);

            var createdGalleriesContainerResponse = await Database.CreateContainerIfNotExistsAsync(Constants.GalleriesContainerName, "/CategoryId");
            var createdGalleriesContainer = createdGalleriesContainerResponse.StatusCode == HttpStatusCode.Created;
            Log.Information("Server.InitialiseDatabaseAsync: Created galleries container? " + createdGalleriesContainer);

            var createdImagesContainerResponse = await Database.CreateContainerIfNotExistsAsync(Constants.ImagesContainerName, "/GalleryId");
            var createdImagesContainer = createdImagesContainerResponse.StatusCode == HttpStatusCode.Created;
            Log.Information("Server.InitialiseDatabaseAsync: Created images container? " + createdImagesContainer);

            var createdUsersContainerResponse = await Database.CreateContainerIfNotExistsAsync(Constants.UsersContainerName, "/PartitionKey");
            var createdUsersContainer = createdUsersContainerResponse.StatusCode == HttpStatusCode.Created;
            Log.Information("Server.InitialiseDatabaseAsync: Created users container? " + createdUsersContainer);
        }

        /// <summary>
        /// Sets up the necessary containers in Azure Blob Storage.
        /// </summary>
        private async Task InitialiseStorageAsync()
        {
            BlobServiceClient = new BlobServiceClient(Configuration["Storage:ConnectionString"]);

            // create containers as necessary
            try
            {
                await BlobServiceClient.CreateBlobContainerAsync(Constants.StorageOriginalContainerName);
                Log.Information($"Server.InitialiseStorageAsync: Created Azure blob storage container: {Constants.StorageOriginalContainerName}");
            }
            catch (RequestFailedException e)
            {
                if (e.ErrorCode == "ContainerAlreadyExists")
                    Log.Information($"Server.InitialiseStorageAsync: Container already exists: {Constants.StorageOriginalContainerName}");
                else
                    // something bad happened
                    throw;
            }

            var spec3840 = ImageFileSpecs.Specs.Single(ifs => ifs.FileSpec == FileSpec.Spec3840);
            try
            {
                await BlobServiceClient.CreateBlobContainerAsync(spec3840.ContainerName);
                Log.Information($"Server.InitialiseStorageAsync: Created Azure blob storage container: {spec3840.ContainerName}");
            }
            catch (RequestFailedException e)
            {
                if (e.ErrorCode == "ContainerAlreadyExists")
                    Log.Information($"Server.InitialiseStorageAsync: Container already exists: {spec3840.ContainerName}");
                else
                    // something bad happened
                    throw;
            }

            var spec1440 = ImageFileSpecs.Specs.Single(ifs => ifs.FileSpec == FileSpec.Spec2560);
            try
            {
                await BlobServiceClient.CreateBlobContainerAsync(spec1440.ContainerName);
                Log.Information($"Server.InitialiseStorageAsync: Created Azure blob storage container: {spec1440.ContainerName}");
            }
            catch (RequestFailedException e)
            {
                if (e.ErrorCode == "ContainerAlreadyExists")
                    Log.Information($"Server.InitialiseStorageAsync: Container already exists: {spec1440.ContainerName}");
                else
                    // something bad happened
                    throw;
            }

            var spec1080 = ImageFileSpecs.Specs.Single(ifs => ifs.FileSpec == FileSpec.Spec1920);
            try
            {
                await BlobServiceClient.CreateBlobContainerAsync(spec1080.ContainerName);
                Log.Information($"Server.InitialiseStorageAsync: Created Azure blob storage container: {spec1080.ContainerName}");
            }
            catch (RequestFailedException e)
            {
                if (e.ErrorCode == "ContainerAlreadyExists")
                    Log.Information($"Server.InitialiseStorageAsync: Container already exists: {spec1080.ContainerName}");
                else
                    // something bad happened
                    throw;
            }

            var spec800 = ImageFileSpecs.Specs.Single(ifs => ifs.FileSpec == FileSpec.Spec800);
            try
            {
                await BlobServiceClient.CreateBlobContainerAsync(spec800.ContainerName);
                Log.Information($"Server.InitialiseStorageAsync: Created Azure blob storage container: {spec800.ContainerName}");
            }
            catch (RequestFailedException e)
            {
                if (e.ErrorCode == "ContainerAlreadyExists")
                    Log.Information($"Server.InitialiseStorageAsync: Container already exists: {spec800.ContainerName}");
                else
                    // something bad happened
                    throw;
            }

            var specLowRes = ImageFileSpecs.Specs.Single(ifs => ifs.FileSpec == FileSpec.SpecLowRes);
            try
            {
                await BlobServiceClient.CreateBlobContainerAsync(specLowRes.ContainerName);
                Log.Information($"Server.InitialiseStorageAsync: Created Azure blob storage container: {specLowRes.ContainerName}");
            }
            catch (RequestFailedException e)
            {
                if (e.ErrorCode == "ContainerAlreadyExists")
                    Log.Information($"Server.InitialiseStorageAsync: Container already exists: {specLowRes.ContainerName}");
                else
                    // something bad happened
                    throw;
            }

            try
            {
                // these pictures can be served straight from their container
                await BlobServiceClient.CreateBlobContainerAsync(Constants.StorageUserPicturesContainerName, PublicAccessType.Blob);
                Log.Information($"Server.InitialiseStorageAsync: Created Azure blob storage container: {Constants.StorageUserPicturesContainerName}");
            }
            catch (RequestFailedException e)
            {
                if (e.ErrorCode == "ContainerAlreadyExists")
                    Log.Information($"Server.InitialiseStorageAsync: Container already exists: {Constants.StorageUserPicturesContainerName}");
                else
                    // something bad happened
                    throw;
            }
        }

        /// <summary>
        /// Sets up the Azure Storage message queues.
        /// </summary>
        private async Task InitialiseQueuesAsync()
        {
            ImageProcessingQueueClient = new QueueClient(Configuration["Storage:ConnectionString"], Constants.QueueImagesToProcess);
            NotificationProcessingQueueClient = new QueueClient(Configuration["Storage:ConnectionString"], Constants.QueueNotificationsToProcess);

            // create the queues
            var imageProcessingQueueResponse = await ImageProcessingQueueClient.CreateIfNotExistsAsync();
            Log.Information(imageProcessingQueueResponse != null
                ? $"Server.InitialiseQueuesAsync: Created {ImageProcessingQueueClient.Name} queue? {imageProcessingQueueResponse.ReasonPhrase}"
                : $"Server.InitialiseQueuesAsync: {ImageProcessingQueueClient.Name} already exists.");

            var notificationsQueueResponse = await NotificationProcessingQueueClient.CreateIfNotExistsAsync();
            Log.Information(notificationsQueueResponse != null
                ? $"Server.InitialiseQueuesAsync: Created {NotificationProcessingQueueClient.Name} queue? {notificationsQueueResponse.ReasonPhrase}"
                : $"Server.InitialiseQueuesAsync: {NotificationProcessingQueueClient.Name} already exists.");
        }
        #endregion
    }
}
