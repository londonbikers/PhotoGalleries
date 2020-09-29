namespace LB.PhotoGalleries.Models.Utilities
{
    public class Constants
    {
        public static string UsersContainerName => "Users";
        public static string GalleriesContainerName => "Galleries";
        public static string ImagesContainerName => "Images";
        public static string CategoriesContainerName => "Categories";

        /// <summary>
        /// All categories go into a single partition due to the expected low volume of them.
        /// </summary>
        public static string CategoriesPartitionKeyValue => "Default";

        /// <summary>
        /// Used as a placeholder when objects become orphaned due to users being deleted.
        /// </summary>
        public static string AnonUserId => "AnonUser";

        /// <summary>
        /// The name of the blob container in Azure Storage for where user-provided images are uploaded to.
        /// </summary>
        public static string StorageOriginalContainerName => "originals";

        /// <summary>
        /// The name of the Azure Storage message queue we use to post-process images on upload.
        /// </summary>
        public static string QueueImagesToProcess => "images-to-process";
    }
}
