using Newtonsoft.Json;
using System;

namespace LB.PhotoGalleries.Application.Models
{
    /// <summary>
    /// A downsized version of the Gallery model. Used in the admin area for displaying the gallery in indexes and other
    /// places where only a few details are needed about the gallery, rather the the whole, heavy object.
    /// </summary>
    public class GalleryAdminStub
    {
        #region accessors
        [JsonProperty("id")]
        public string Id { get; set; }
        public string CategoryId { get; set; }
        public string Name { get; set; }
        /// <summary>
        /// Inactive photo galleries are not displayed to users.
        /// </summary>
        public bool Active { get; set; }
        /// <summary>
        /// The ordered list of images in the photo gallery.
        /// </summary>
        public DateTime Created { get; set; }
        #endregion

        #region public methods
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
        #endregion
    }
}
