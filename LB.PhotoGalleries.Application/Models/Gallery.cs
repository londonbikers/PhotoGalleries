using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LB.PhotoGalleries.Application.Models
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
        /// The ordered list of images in the photo gallery.
        /// </summary>
        public Dictionary<int, Image> Images { get; set;}
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
        /// The partition key of the user who created the gallery.
        /// </summary>
        public string CreatedByUserPartitionKey { get; set; }
        /// <summary>
        /// The numeric id of the gallery when it was stored in the old londonbikers_v5 database.
        /// Useful for URL conversion/redirects.
        /// </summary>
        [DisplayName("Legacy Number ID")]
        public int LegacyNumId { get; set; }
        /// <summary>
        /// The guid id of the gallery when it was stored in the old londonbikers_v5 database.
        /// Useful for URL conversion/redirects.
        /// </summary>
        [DisplayName("Legacy Guid ID")]
        public Guid LegacyGuidId { get; set; }
        [Required]
        public DateTime Created { get; set; }
        #endregion

        #region constructors
        public Gallery()
        {
            Images = new Dictionary<int, Image>();
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
                string.IsNullOrEmpty(Description) ||
                string.IsNullOrEmpty(CategoryId) ||
                string.IsNullOrEmpty(CreatedByUserId))
                return false;

            return true;
        }
        #endregion
    }
}
