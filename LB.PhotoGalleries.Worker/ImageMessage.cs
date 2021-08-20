using LB.PhotoGalleries.Models.Enums;

namespace LB.PhotoGalleries.Worker
{
    public class ImageMessage
    {
        public WorkerOperation Operation { get; set; }
        public string ImageId { get; set; }
        public string GalleryId { get; set; }
        public string GalleryCategoryId { get; set; }
        public bool OverwriteImageProperties { get; set; }
    }
}
