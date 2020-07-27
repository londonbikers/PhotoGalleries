using System;
using System.Collections.Generic;

namespace LB.PhotoGalleries.Application.Models
{
    public class Image
    {
        #region accessors
        public int Id { get; set; }
        public string Name { get; set; }
        /// <summary>
        /// Descriptive text for the photo
        /// </summary>
        public string Caption { get; set; }
        /// <summary>
        /// A credit for the photo, i.e. who took it.
        /// </summary>
        public string Credit { get; set; }
        public DateTime CaptureDate { get; set; }
        /// <summary>
        /// The numeric id of the image when it was stored in the old londonbikers_v5 database.
        /// Useful for URL conversion/redirects.
        /// </summary>
        public int LegacyNumId { get; set; }
        /// <summary>
        /// The guid id of the image when it was stored in the old londonbikers_v5 database.
        /// Useful for URL conversion/redirects.
        /// </summary>
        public Guid LegacyGuidId { get; set; }
        public List<Comment> Comments { get; set; }
        #endregion
        
        #region constructors
        public Image()
        {
            Comments = new List<Comment>();
        }
        #endregion
    }
}
