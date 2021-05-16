﻿using LB.PhotoGalleries.Models.Enums;

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

        /// <summary>
        /// The name of the Azure Blob storage container these files reside in.
        /// </summary>
        public string ContainerName { get; set; }

        #region constructors
        public ImageFileSpec(FileSpec fileSpec, int pixelLength, int quality, string containerName)
        {
            FileSpec = fileSpec;
            PixelLength = pixelLength;
            Quality = quality;
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
