using Newtonsoft.Json;

namespace LB.PhotoGalleries.CommentCounter.Models
{
    internal class GalleryCommentCountStub
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string CategoryId { get; set; }
        public int CommentCount { get; set; }
    }
}