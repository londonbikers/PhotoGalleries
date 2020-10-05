using System;

namespace LB.PhotoGalleries.Models.Exceptions
{
    public class ImageTooSmallException : Exception
    {
        public ImageTooSmallException(string message) : base(message)
        {
        }
    }
}
