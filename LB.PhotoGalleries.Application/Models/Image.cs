using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LB.PhotoGalleries.Application.Models
{
    public class Image
    {
        #region accessors
        [JsonProperty("id")]
        public string Id { get; set; }
        [DisplayName("Gallery Id")]
        public string GalleryId { get; set; }
        /// <summary>
        /// The unique identifier for the blob in Azure Blob storage.
        /// </summary>
        [DisplayName("Storage Id")]
        public string StorageId { get; set; }
        /// <summary>
        /// The position of the image in the gallery.
        /// </summary>
        public int? Position { get; set; }
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
        /// When the image was created, not when the photo was originally captured.
        /// </summary>
        public DateTime Created { get; set; }
        /// <summary>
        /// The numeric id of the image when it was stored in the old londonbikers_v5 database.
        /// Useful for URL conversion/redirects.
        /// </summary>
        [DisplayName("Legacy Number Id")]
        public int? LegacyNumId { get; set; }
        /// <summary>
        /// The guid id of the image when it was stored in the old londonbikers_v5 database.
        /// Useful for URL conversion/redirects.
        /// </summary>
        [DisplayName("Legacy Guid Id")]
        public Guid? LegacyGuidId { get; set; }
        public List<Comment> Comments { get; set; }
        /// <summary>
        /// Tags that define the context of the photo, i.e. what's in it, where it is, etc.
        /// </summary>
        public List<string> Tags { get; set; }

        public Metadata Metadata { get; set; }
        #endregion
        
        #region constructors
        public Image()
        {
            Created = DateTime.Now;
            Comments = new List<Comment>();
            Tags = new List<string>();
            Metadata = new Metadata();
        }
        #endregion

        #region public methods
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(Id) ||
                string.IsNullOrEmpty(StorageId) ||
                string.IsNullOrEmpty(GalleryId) ||
                string.IsNullOrEmpty(Name))
                return false;

            return true;
        }
        #endregion
    }

    /// <summary>
    /// Store the most popular metadata in the document with the image.
    /// The full metadata is always available by reading the original file from storage.
    /// </summary>
    public class Metadata
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string CameraMake { get; set; }
        public string CameraModel { get; set; }
        public string ExposureTime { get; set; }
        public int? Iso { get; set; }
        public DateTime? TakenDate { get; set; }
        public string Aperture { get; set; }
        public string ExposureBias { get; set; }
        public string MeteringMode { get; set; }
        public string Flash { get; set; }
        public string FocalLength { get; set; }
        public string WhiteBalance { get; set; }
        public string WhiteBalanceMode { get; set; }
        public string LensMake { get; set; }
        public string LensModel { get; set; }
        public double? LocationLatitude { get; set; }
        public double? LocationLongitude { get; set; }
    }
}
