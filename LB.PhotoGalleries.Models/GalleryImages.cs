using System.Collections.Generic;

namespace LB.PhotoGalleries.Models
{
    /// <summary>
    /// Used to contain gallery images so that they can be cached and retrieved easily without having to go to the database each time.
    /// </summary>
    public class GalleryImages
    {
        public string GalleryId { get; set; }
        public List<Image> Images { get; set; }
    }
}
