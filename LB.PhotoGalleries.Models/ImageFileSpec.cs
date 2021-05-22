using LB.PhotoGalleries.Models.Enums;

namespace LB.PhotoGalleries.Models
{
    /// <summary>
    /// Defines an image file specification we use to create and retrieve pre-generated images.
    /// </summary>
    public class ImageFileSpec
    {
        /// <summary>
        /// The id for this image file spec.
        /// </summary>
        public FileSpec FileSpec { get; set; }

        /// <summary>
        /// The length in pixels along the longest side of the image.
        /// </summary>
        public int PixelLength { get; set; }

        /// <summary>
        /// The level of quality the image should be created at (0-100).
        /// </summary>
        public int Quality { get; set; }

        public FileSpecFormat FileSpecFormat { get; set; }

        /// <summary>
        /// The level of sharpening to apply. Should be a value between 0 and 100;
        /// </summary>
        public float SharpeningAmount { get; set; }

        /// <summary>
        /// The name of the Azure Blob storage container these files reside in.
        /// </summary>
        public string ContainerName { get; set; }

        #region constructors
        public ImageFileSpec(FileSpec fileSpec, FileSpecFormat fileSpecFormat, int pixelLength, int quality, float sharpeningAmount, string containerName)
        {
            FileSpec = fileSpec;
            FileSpecFormat = fileSpecFormat;
            PixelLength = pixelLength;
            Quality = quality;
            SharpeningAmount = sharpeningAmount;
            ContainerName = containerName;
        }
        #endregion

        #region internal methods
        public string GetStorageId(Image image)
        {
            return image.Id + ".jpg";
        }
        #endregion
    }
}
