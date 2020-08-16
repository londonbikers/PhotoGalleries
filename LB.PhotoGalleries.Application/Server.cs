using Azure;
using Azure.Storage.Blobs;
using LB.PhotoGalleries.Application.Servers;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
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
        public UserServer Users { get; internal set; }
        /// <summary>
        /// Provides access to application configuration data, i.e. connection strings.
        /// Needs to be set on startup by the client using SetConfiguration().
        /// </summary>
        internal IConfiguration Configuration { get; set; }
        internal CosmosClient CosmosClient { get; set; }
        internal Database Database { get; set; }
        #endregion

        #region constructors
        static Server()
        {
            Instance = new Server
            {
                Categories = new CategoryServer(),
                Galleries = new GalleryServer(),
                Users = new UserServer()
            };
        }
        #endregion

        #region public method
        /// <summary>
        /// Must be set by the client consuming Server before the server can be used so it can obtain connection strings, etc.
        /// i.e. Set in Startup.cs for an MVC application.
        /// </summary>
        public async Task SetConfigurationAsync(IConfiguration configuration)
        {
            Configuration = configuration;
            await InitialiseDatabaseAsync();
            await InitialiseStorageAsync();
        }
        #endregion

        #region private methods
        private async Task InitialiseDatabaseAsync()
        {
            // authenticate with the CosmosDB service and create a client we can re-use
            CosmosClient = new CosmosClient(Configuration["CosmosDB:Uri"], Configuration["CosmosDB:PrimaryKey"]);

            // create the CosmosDB database if it doesn't already exist
            var response = await CosmosClient.CreateDatabaseIfNotExistsAsync(Constants.DatabaseName);
            var createdDatabase = response.StatusCode == HttpStatusCode.Created;
            Debug.WriteLine("Server.InitialiseDatabaseAsync: Created database? " + createdDatabase);

            // keep a reference to the database so other parts of the app can easily interact with it
            Database = response.Database;

            // create containers for all our top level objects we want to persist
            var createdCategoriesContainerResponse = await Database.CreateContainerIfNotExistsAsync(Constants.CategoriesContainerName, "/PartitionKey");
            var createdCategoriesContainer = createdCategoriesContainerResponse.StatusCode == HttpStatusCode.Created;
            Debug.WriteLine("Server.InitialiseDatabaseAsync: Created categories container? " + createdCategoriesContainer);

            var createdGalleriesContainerResponse = await Database.CreateContainerIfNotExistsAsync(Constants.GalleriesContainerName, "/CategoryId");
            var createdGalleriesContainer = createdGalleriesContainerResponse.StatusCode == HttpStatusCode.Created;
            Debug.WriteLine("Server.InitialiseDatabaseAsync: Created galleries container? " + createdGalleriesContainer);

            var createdUsersContainerResponse = await Database.CreateContainerIfNotExistsAsync(Constants.UsersContainerName, "/PartitionKey");
            var createdUsersContainer = createdUsersContainerResponse.StatusCode == HttpStatusCode.Created;
            Debug.WriteLine("Server.InitialiseDatabaseAsync: Created users container? " + createdUsersContainer);
        }

        /// <summary>
        /// Sets up the necessary containers in Azure Blob Storage.
        /// </summary>
        private async Task InitialiseStorageAsync()
        {
            var storageConnectionString = Configuration["Storage:ConnectionString"];
            var blobServiceClient = new BlobServiceClient(storageConnectionString);

            // create containers as necessary
            try
            {
                await blobServiceClient.CreateBlobContainerAsync(Constants.StorageOriginalContainerName);
                Debug.WriteLine($"Server.InitialiseStorageAsync: Created Azure blob storage container: {Constants.StorageOriginalContainerName}");
            }
            catch (RequestFailedException e)
            {
                if (e.ErrorCode == "ContainerAlreadyExists")
                    Debug.WriteLine($"Server.InitialiseStorageAsync: Container already exists: {Constants.StorageOriginalContainerName}");
                else
                    // something bad happened
                    throw;
            }
        }
        #endregion
    }
}
