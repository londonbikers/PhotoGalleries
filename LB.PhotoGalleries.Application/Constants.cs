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

        /// <summary>
        /// All categories go into a single partition due to the expected low volume of them.
        /// </summary>
        internal static string CategoriesPartitionKeyValue => "Default";

        /// <summary>
        /// Used as a placeholder when objects become orphaned due to users being deleted.
        /// </summary>
        internal static string AnonUserId => "AnonUser";
    }
}
