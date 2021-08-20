using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LB.PhotoGalleries.Models
{
    public class Image
    {
        #region accessors
        [JsonProperty("id")]
        public string Id { get; set; }

        [DisplayName("Gallery Category Id")]
        public string GalleryCategoryId { get; set; }

        [DisplayName("Gallery Id")]
        public string GalleryId { get; set; }

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
        public long? LegacyNumId { get; set; }

        /// <summary>
        /// The guid id of the image when it was stored in the old londonbikers_v5 database.
        /// Useful for URL conversion/redirects.
        /// </summary>
        [DisplayName("Legacy Guid Id")]
        public Guid? LegacyGuidId { get; set; }

        /// <summary>
        /// Comments that users have left against this image.
        /// </summary>
        public List<Comment> Comments { get; set; }

        /// <summary>
        /// A CSV of tags that define the context of the photo, i.e. what's in it, where it is, etc. 
        /// </summary>
        public string TagsCsv { get; set; }

        /// <summary>
        /// The Exif and IPTC metadata that we have parsed out of an image.
        /// </summary>
        public Metadata Metadata { get; set; }

        /// <summary>
        /// The different versions of files we have for this image.
        /// We have different images for different size screens and use-cases.
        /// </summary>
        public ImageFiles Files { get; set; }

        /// <summary>
        /// How many times this image has been viewed by people
        /// </summary>
        public long Views { get; set; }

        /// <summary>
        /// Contains the IDs of users who have subscribed to comment notifications for this image.
        /// </summary>
        public List<string> UserCommentSubscriptions { get; set; }
        #endregion

        #region constructors
        public Image()
        {
            Created = DateTime.Now;
            Comments = new List<Comment>();
            Metadata = new Metadata();
            Files = new ImageFiles();
            UserCommentSubscriptions = new List<string>();
        }
        #endregion

        #region public methods
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(Id) ||
                string.IsNullOrEmpty(Files.OriginalId) ||
                string.IsNullOrEmpty(GalleryId) ||
                string.IsNullOrEmpty(GalleryCategoryId) ||
                string.IsNullOrEmpty(Name))
                return false;

            return true;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        #endregion
    }

    /// <summary>
    /// Store the most popular metadata in the document with the image.
    /// The full metadata is always available by reading the original file from storage.
    /// </summary>
    public class Metadata
    {
        /// <summary>
        /// The date the metadata was last processed for this image.
        /// </summary>
        [DisplayName("Date Last Processed")]
        public DateTime? DateLastProcessed { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        [DisplayName("Camera Make")]
        public string CameraMake { get; set; }

        [DisplayName("Camera Model")]
        public string CameraModel { get; set; }

        [DisplayName("Exposure Time")]
        public string ExposureTime { get; set; }

        public int? Iso { get; set; }
        [DisplayName("Taken Date")]

        public DateTime? TakenDate { get; set; }

        public string Aperture { get; set; }

        [DisplayName("Exposure Bias")]
        public string ExposureBias { get; set; }

        [DisplayName("Metering Mode")]
        public string MeteringMode { get; set; }

        public string Flash { get; set; }

        [DisplayName("Focal Length")]
        public string FocalLength { get; set; }

        [DisplayName("White Balance")]
        public string WhiteBalance { get; set; }

        [DisplayName("White Balance Mode")]
        public string WhiteBalanceMode { get; set; }

        [DisplayName("Lens Make")]
        public string LensMake { get; set; }

        [DisplayName("Lens Model")]
        public string LensModel { get; set; }

        [DisplayName("Location Latitude")]
        public double? LocationLatitude { get; set; }

        [DisplayName("Location Longitude")]
        public double? LocationLongitude { get; set; }
        public string City { get; set; }
        public string Location { get; set; }
        public string State { get; set; }
        public string Country { get; set; }

        [DisplayName("Original Filename")]
        public string OriginalFilename { get; set; }

        /// <summary>
        /// Gets a comma-separated version of the different location metadata properties.
        /// </summary>
        [JsonIgnore]
        public string CombinedLocation
        {
            get
            {
                // location or city are required.
                // state and country are too high-level to be of use on a map.
                if (string.IsNullOrEmpty(Location) && string.IsNullOrEmpty(City))
                    return null;

                var components = new List<string>();
                if (!string.IsNullOrEmpty(Location))
                    components.Add(Location);
                if (!string.IsNullOrEmpty(City))
                    components.Add(City);
                if (!string.IsNullOrEmpty(State))
                    components.Add(State);
                if (!string.IsNullOrEmpty(Country))
                    components.Add(Country);
                var combinedLocation = string.Join(',', components);
                return combinedLocation;
            }
        }
    }

    public class ImageFiles
    {
        /// <summary>
        /// The storage id for the file that was originally uploaded.
        /// Expected to be very big at times.
        /// </summary>
        [DisplayName("Original file id")]
        public string OriginalId { get; set; }

        /// <summary>
        /// The storage id for the UHD (3840 × 2160) version of the image.
        /// Meant for displaying main images on high-resolution screens.
        /// </summary>
        [DisplayName("3840 pixel file id")]
        public string Spec3840Id { get; set; }

        /// <summary>
        /// The storage id for the 2560 pixel long version of the image.
        /// Meant for displaying main images on high-resolution screens.
        /// </summary>
        [DisplayName("2560 pixel file id")]
        public string Spec2560Id { get; set; }

        /// <summary>
        /// The storage id for the 1920 pixel long version of the image.
        /// Meant for displaying main images on normal screens and high dpi mobile devices.
        /// </summary>
        [DisplayName("1920 pixel file id")]
        public string Spec1920Id { get; set; }

        /// <summary>
        /// The storage id for the 800 pixel long version of the image.
        /// Meant for displaying thumbnails on up to 2x device pixel ratio screens.
        /// </summary>
        [DisplayName("800 pixel file id")]
        public string Spec800Id { get; set; }

        /// <summary>
        /// The storage id for the 400 pixel long at 50% quality jpeg version of the image.
        /// We use this to very roughly show the main image before the larger, full quality one loads in from storage.
        /// </summary>
        [DisplayName("Low-res file id")]
        public string SpecLowResId { get; set; }
    }
}
