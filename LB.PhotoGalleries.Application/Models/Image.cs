﻿using System;
using System.Collections.Generic;

namespace LB.PhotoGalleries.Application.Models
{
    public class Image
    {
        #region accessors
        /// <summary>
        /// Will be the unique identifier of the image file in storage.
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Descriptive name for the photo to be shown to users.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Descriptive text for the photo to be shown to users.
        /// </summary>
        public string Caption { get; set; }
        /// <summary>
        /// A credit for the photo, i.e. who took it.
        /// </summary>
        public string Credit { get; set; }
        /// <summary>
        /// When the photo was originally taken.
        /// </summary>
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
        /// <summary>
        /// Tags that define the context of the photo, i.e. what's in it, where it is, etc.
        /// </summary>
        public List<string> Tags { get; set; }
        #endregion
        
        #region constructors
        public Image()
        {
            Comments = new List<Comment>();
            Tags = new List<string>();
        }
        #endregion
    }
}
