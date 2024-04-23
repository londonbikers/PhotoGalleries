using System;

namespace LB.PhotoGalleries.Models.Exceptions;

public class ImageNotFoundException : Exception
{
    public string ImageId { get; set; }
    public string GalleryId { get; set; }

    public ImageNotFoundException(string message) : base(message)
    {
    }
}
