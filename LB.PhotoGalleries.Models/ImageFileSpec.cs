using System;
using System.IO;
using Imageflow.Fluent;
using LB.PhotoGalleries.Models.Enums;

namespace LB.PhotoGalleries.Models;

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
    public uint PixelLength { get; set; }

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
    /// The filter to use when sharpening images.
    /// </summary>
    public InterpolationFilter InterpolationFilter { get; set; }

    /// <summary>
    /// The name of the Azure Blob storage container these files reside in.
    /// </summary>
    public string ContainerName { get; set; }

    #region constructors
    public ImageFileSpec(FileSpec fileSpec, string containerName)
    {
        FileSpec = fileSpec;
        ContainerName = containerName;
        FileSpecFormat = FileSpecFormat.Undefined;
            
        // this will never be used for this simple constructor, it's just that a valid is needed.
        InterpolationFilter = InterpolationFilter.Robidoux;
    }

    public ImageFileSpec(FileSpec fileSpec, FileSpecFormat fileSpecFormat, uint pixelLength, string containerName)
    {
        FileSpec = fileSpec;
        FileSpecFormat = fileSpecFormat;
        PixelLength = pixelLength;
        InterpolationFilter = InterpolationFilter.Robidoux;
        ContainerName = containerName;
    }

    public ImageFileSpec(FileSpec fileSpec, FileSpecFormat fileSpecFormat, uint pixelLength, int quality, string containerName)
    {
        FileSpec = fileSpec;
        FileSpecFormat = fileSpecFormat;
        PixelLength = pixelLength;
        Quality = quality;
        InterpolationFilter = InterpolationFilter.Robidoux;
        ContainerName = containerName;
    }

    public ImageFileSpec(FileSpec fileSpec, FileSpecFormat fileSpecFormat, uint pixelLength, int quality, float sharpeningAmount, InterpolationFilter interpolationFilter, string containerName)
    {
        FileSpec = fileSpec;
        FileSpecFormat = fileSpecFormat;
        PixelLength = pixelLength;
        Quality = quality;
        SharpeningAmount = sharpeningAmount;
        InterpolationFilter = interpolationFilter;
        ContainerName = containerName;
    }
    #endregion

    public string GetStorageId(Image image)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));

        if (string.IsNullOrEmpty(image.Files.OriginalId))
            throw new ArgumentException("image.Files.OriginalId is empty!");

        // take the id from the original image, as when we allow image-replacements, the original image id will change
        // and so all new generated images need to change name as well to break client caches.
        var id = Path.GetFileNameWithoutExtension(image.Files.OriginalId);
        if (FileSpecFormat == FileSpecFormat.Jpeg)
            return id + ".jpg";
            
        return id + ".webp";
    }

    public override string ToString()
    {
        return FileSpecFormat == FileSpecFormat.WebPLossless ? 
            $"{FileSpec}-{FileSpecFormat}-{PixelLength}px" : 
            $"{FileSpec}-{FileSpecFormat}-{PixelLength}px-{Quality}q-{SharpeningAmount}s";
    }
}
