using LB.PhotoGalleries.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace LB.PhotoGalleries.ImageCommentCounter.Models
{
    internal class ImageStub
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string GalleryId { get; set; }
        public List<Comment> Comments { get; set; }
    }
}