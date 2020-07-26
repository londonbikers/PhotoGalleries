namespace LB.PhotoGalleries.Application
{
    internal class Constants
    {
        /// <summary>
        /// The name of the CosmosDB database to use to store application data.
        /// </summary>
        internal static string DatabaseName => "PhotoGalleries";
        internal static string UsersContainerName => "Users";
        internal static string GalleriesContainerName => "Galleries";
        internal static string CategoriesContainerName => "Categories";
    }
}