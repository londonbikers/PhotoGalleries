using Newtonsoft.Json;

namespace LB.PhotoGalleries.CommentCounter.Models
{
    internal class ImageCommentCountStub
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string GalleryId { get; set; }
        public string GalleryCategoryId { get; set; }
        public int CommentCount { get; set; }
    }
}