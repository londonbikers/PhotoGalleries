namespace LB.PhotoGalleries.Application
{
    public class Constants
    {
        internal static string UsersContainerName => "Users";
        internal static string GalleriesContainerName => "Galleries";
        internal static string ImagesContainerName => "Images";
        internal static string CategoriesContainerName => "Categories";

        /// <summary>
        /// All categories go into a single partition due to the expected low volume of them.
        /// </summary>
        internal static string CategoriesPartitionKeyValue => "Default";

        /// <summary>
        /// Used as a placeholder when objects become orphaned due to users being deleted.
        /// </summary>
        internal static string AnonUserId => "AnonUser";

        /// <summary>
        /// The name of the blob container in Azure Storage for where user-provided images are uploaded to.
        /// </summary>
        public static string StorageOriginalContainerName => "originals";

        /// <summary>
        /// The name of the blob container in Azure Storage for where the low-resolution images we generate are uploaded to.
        /// </summary>
        public static string StorageLowResContainerName => "lowres";
    }
}
