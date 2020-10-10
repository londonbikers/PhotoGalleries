using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LB.PhotoGalleries.Models
{
    public class Gallery
    {
        #region accessors
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [Required]
        [DisplayName("Category")]
        public string CategoryId { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        public string Description { get; set; }
        
        /// <summary>
        /// Inactive photo galleries are not displayed to users.
        /// </summary>
        public bool Active { get; set; }
        
        /// <summary>
        /// Comments can be made by users against photo galleries themselves as well as on specific photos.
        /// </summary>
        public List<Comment> Comments { get; set; }
        
        /// <summary>
        /// The id of the user who created the gallery.
        /// </summary>
        [Required]
        [DisplayName("Created by")]
        public string CreatedByUserId { get; set; }
        
        /// <summary>
        /// The numeric id of the gallery when it was stored in the old londonbikers_v5 database.
        /// Useful for URL conversion/redirects.
        /// </summary>
        [DisplayName("Legacy Number ID")]
        public long? LegacyNumId { get; set; }
        
        /// <summary>
        /// The guid id of the gallery when it was stored in the old londonbikers_v5 database.
        /// Useful for URL conversion/redirects.
        /// </summary>
        [DisplayName("Legacy Guid ID")]
        
        public Guid? LegacyGuidId { get; set; }
        [Required]
        
        public DateTime Created { get; set; }

        /// <summary>
        /// A copy of the Image.Files collection of file storage ids for the thumbnail image.
        /// </summary>
        /// <remarks>
        /// Saves on having to request the gallery and an image from the database.
        /// </remarks>
        public ImageFiles ThumbnailFiles { get; set; }
        
        /// <summary>
        /// The number of images in this gallery.
        /// </summary>
        /// <remarks>
        /// Needs to be updated on completion of each image upload batch or any gallery update.
        /// </remarks>
        [DisplayName("Image Count")]
        public int ImageCount { get; set; }
        #endregion

        #region constructors
        public Gallery()
        {
            Comments = new List<Comment>();
            Created = DateTime.Now;
        }
        #endregion

        #region public methods
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public bool IsValid()
        {
            if (string.IsNullOrEmpty(Id) ||
                string.IsNullOrEmpty(Name) ||
                string.IsNullOrEmpty(CategoryId))
                return false;

            return true;
        }
        #endregion
    }
}
