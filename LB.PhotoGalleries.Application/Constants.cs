namespace LB.PhotoGalleries.Application
{
    internal class Constants
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
        internal static string StorageOriginalContainerName => "originals";

        /// <summary>
        /// The name of the Azure Storage message queue we use to post-process images on upload.
        /// </summary>
        internal static string QueueImagesToProcess => "images-to-process";
    }

    /// <summary>
    /// Names of file specs we use to pre-generate images for.
    /// </summary>
    public enum FileSpec
    {
        SpecOriginal,
        Spec3840,
        Spec2560,
        Spec1920,
        Spec800,
        SpecLowRes
    }
}
