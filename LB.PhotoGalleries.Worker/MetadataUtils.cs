using LB.PhotoGalleries.Shared;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Xmp;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Directory = MetadataExtractor.Directory;
using Image = LB.PhotoGalleries.Models.Image;

namespace LB.PhotoGalleries.Worker
{
    public class MetadataUtils
    {
        /// <summary>
        /// Images contain metadata that describes the photo to varying degrees. This method extracts the metadata
        /// and parses out the most interesting pieces we're interested in and assigns it to the image object so we can
        /// present the information to the user and use it to help with searches.
        /// </summary>
        /// <param name="image">The Image object to assign the metadata to.</param>
        /// <param name="imageBytes">The byte array containing the recently-uploaded image file to inspect for metadata.</param>
        /// <param name="overwriteImageProperties">Specifies whether or not to update image properties from metadata that already have values.</param>
        /// <param name="log">Optionally pass in an ILogger instance to enable internal logging</param>
        public static void ParseAndAssignImageMetadata(Image image, byte[] imageBytes, bool overwriteImageProperties, ILogger log = null)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            using var imageStream = new MemoryStream(imageBytes);
            
            // whilst image dimensions can be extracted from metadata in some cases, not in every case and this isn't acceptable
            var bitmap = new Bitmap(imageStream);
            image.Metadata.Width = bitmap.Width;
            image.Metadata.Height = bitmap.Height;

            imageStream.Position = 0;
            var directories = ImageMetadataReader.ReadMetadata(imageStream);
            image.Metadata.TakenDate = GetImageDateTaken(directories);
            if (image.Metadata.TakenDate.HasValue)
            {
                // overwrite the image created date with when the photo was taken
                image.Created = image.Metadata.TakenDate.Value;
            }

            var iso = GetImageIso(directories);
            if (iso.HasValue)
                image.Metadata.Iso = iso.Value;

            if (!image.Credit.HasValue() || overwriteImageProperties)
            {
                var credit = GetImageCredit(directories);
                if (credit.HasValue())
                    image.Credit = credit;
            }

            var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (exifIfd0Directory != null)
            {
                var make = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagMake);
                if (make != null && make.Description.HasValue())
                    image.Metadata.CameraMake = make.Description;

                var model = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagModel);
                if (model != null && model.Description.HasValue())
                    image.Metadata.CameraModel = model.Description;

                if (!image.Caption.HasValue() || overwriteImageProperties)
                {
                    var imageDescription = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagImageDescription);
                    if (imageDescription != null && imageDescription.Description.HasValue())
                        image.Caption = imageDescription.Description;
                }
            }

            var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfdDirectory != null)
            {
                var exposureTime = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagExposureTime);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (exposureTime != null && exposureTime.Description.HasValue())
                    image.Metadata.ExposureTime = exposureTime.Description;

                var aperture = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagAperture);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (aperture != null && aperture.Description.HasValue())
                    image.Metadata.Aperture = aperture.Description;

                var exposureBias = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagExposureBias);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (exposureBias != null && exposureBias.Description.HasValue())
                    image.Metadata.ExposureBias = exposureBias.Description;

                var meteringMode = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagMeteringMode);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (meteringMode != null && meteringMode.Description.HasValue())
                    image.Metadata.MeteringMode = meteringMode.Description;

                var flash = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagFlash);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (flash != null && flash.Description.HasValue())
                    image.Metadata.Flash = flash.Description;

                var focalLength = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagFocalLength);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (focalLength != null && focalLength.Description.HasValue())
                    image.Metadata.FocalLength = focalLength.Description;

                var lensMake = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagLensMake);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (lensMake != null && lensMake.Description.HasValue())
                    image.Metadata.LensMake = lensMake.Description;

                var lensModel = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagLensModel);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (lensModel != null && lensModel.Description.HasValue())
                    image.Metadata.LensModel = lensModel.Description;

                var whiteBalance = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagWhiteBalance);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (whiteBalance != null && whiteBalance.Description.HasValue())
                    image.Metadata.WhiteBalance = whiteBalance.Description;

                var whiteBalanceMode = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagWhiteBalanceMode);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (whiteBalanceMode != null && whiteBalanceMode.Description.HasValue())
                    image.Metadata.WhiteBalanceMode = whiteBalanceMode.Description;
            }

            var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();
            var location = gpsDirectory?.GetGeoLocation();
            if (location != null)
            {
                image.Metadata.LocationLatitude = location.Latitude;
                image.Metadata.LocationLongitude = location.Longitude;
            }

            var iptcDirectory = directories.OfType<IptcDirectory>().FirstOrDefault();
            if (iptcDirectory != null)
            {
                var objectName = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagObjectName);
                if (objectName != null && objectName.Description.HasValue())
                    image.Name = Utilities.TidyImageName(objectName.Description);

                var keywords = iptcDirectory.GetKeywords();
                if (keywords != null)
                    foreach (var keyword in keywords.Where(k => k.HasValue()))
                        image.TagsCsv = Utilities.AddTagToCsv(image.TagsCsv, keyword.ToLower().Trim());

                if (!image.Metadata.Location.HasValue() || overwriteImageProperties)
                {
                    var objectLocation = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagSubLocation);
                    if (objectLocation != null && objectLocation.Description.HasValue())
                        image.Metadata.Location = objectLocation.Description;
                }

                if (!image.Metadata.City.HasValue() || overwriteImageProperties)
                {
                    var objectCity = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagCity);
                    if (objectCity != null && objectCity.Description.HasValue())
                        image.Metadata.City = objectCity.Description;
                }

                if (!image.Metadata.State.HasValue() || overwriteImageProperties)
                {
                    var objectState = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagProvinceOrState);
                    if (objectState != null && objectState.Description.HasValue())
                        image.Metadata.State = objectState.Description;
                }

                if (!image.Metadata.Country.HasValue() || overwriteImageProperties)
                {
                    var objectCountry = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagCountryOrPrimaryLocationName);
                    if (objectCountry != null && objectCountry.Description.HasValue())
                        image.Metadata.Country = objectCountry.Description;
                }
            }

            var xmpDirectory = directories.OfType<XmpDirectory>().FirstOrDefault();
            if (xmpDirectory != null)
            {
                // see if there's any tags (subjects in xmp)
                // this will just add tags, so if we're re-processing, we won't lose any manually-entered ones
                // or cause duplications.
                var subjects = xmpDirectory.XmpMeta.Properties.Where(q => q.Path != null && q.Path.StartsWith("dc:subject") && !string.IsNullOrEmpty(q.Value));
                foreach (var subject in subjects)
                    image.TagsCsv = Utilities.AddTagToCsv(image.TagsCsv, subject.Value);

                // sometimes we have no camera info, but sometimes something can be inferred from lens profile info
                var lensProfileFilename = xmpDirectory.XmpMeta.GetPropertyString("http://ns.adobe.com/camera-raw-settings/1.0/", "crs:LensProfileFilename");
                if (!string.IsNullOrEmpty(lensProfileFilename) && string.IsNullOrEmpty(image.Metadata.CameraModel))
                {
                    var camera = lensProfileFilename.Substring(0, lensProfileFilename.IndexOf(" (", StringComparison.Ordinal));
                    image.Metadata.CameraModel = camera;
                }

                var lensProfileName = xmpDirectory.XmpMeta.GetPropertyString("http://ns.adobe.com/camera-raw-settings/1.0/", "crs:LensProfileName");
                if (!string.IsNullOrEmpty(lensProfileName) && string.IsNullOrEmpty(image.Metadata.LensModel))
                {
                    var lens = Regex.Match(lensProfileName, @"\((.*?)\)", RegexOptions.Compiled);
                    if (lens.Success && lens.Value.Contains("(") && lens.Value.Contains(")"))
                        image.Metadata.LensModel = lens.Value.TrimStart('(').TrimEnd(')');
                }
            }

            image.Metadata.DateLastProcessed = DateTime.Now;
            stopwatch.Stop();
            log?.Information($"LB.PhotoGalleries.Worker.MetadataUtils.ParseAndAssignImageMetadata() - Processed metadata for {image.Id} in {stopwatch.ElapsedMilliseconds}ms");
        }

        #region private methods
        /// <summary>
        /// Attempts to extract and parse image date capture information from image metadata.
        /// </summary>
        /// <param name="directories">Metadata directories extracted from the image stream.</param>
        private static DateTime? GetImageDateTaken(IEnumerable<Directory> directories)
        {
            // obtain the Exif SubIFD directory
            var directory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (directory == null)
                return null;

            // query the tag's value
            if (directory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateTime))
                return dateTime;

            return null;
        }

        /// <summary>
        /// Attempts to extract image iso information from image metadata.
        /// </summary>
        /// <param name="directories">Metadata directories extracted from the image stream.</param>
        private static int? GetImageIso(IEnumerable<Directory> directories)
        {
            int iso;
            var enumerable = directories as Directory[] ?? directories.ToArray();
            var exifSubIfdDirectory = enumerable.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfdDirectory != null)
            {
                var isoTag = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagIsoEquivalent);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (isoTag != null && isoTag.Description.HasValue())
                {
                    var validIso = int.TryParse(isoTag.Description, out iso);
                    if (validIso)
                        return iso;

                    Log.Debug($"ImageServer.GetImageIso: ExifSubIfdDirectory iso tag value wasn't an int: '{isoTag.Description}'");
                }
            }

            var nikonDirectory = enumerable.OfType<NikonType2MakernoteDirectory>().FirstOrDefault();
            // ReSharper disable once InvertIf
            if (nikonDirectory != null)
            {
                var isoTag = nikonDirectory.Tags.SingleOrDefault(t => t.Type == NikonType2MakernoteDirectory.TagIso1);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (isoTag == null || !isoTag.Description.HasValue())
                    return null;

                if (isoTag.Description == null || !isoTag.Description.StartsWith("ISO "))
                    return null;

                var isoTagProcessed = isoTag.Description.Split(' ')[1];
                var validIso = int.TryParse(isoTagProcessed, out iso);
                if (validIso)
                    return iso;

                Log.Debug($"ImageServer.GetImageIso: NikonType2MakernoteDirectory iso tag value wasn't an int: '{isoTag.Description}'");
            }

            return null;
        }

        /// <summary>
        /// Attempts to extract image credit information from image metadata.
        /// </summary>
        /// <param name="directories">Metadata directories extracted from the image stream.</param>
        private static string GetImageCredit(IReadOnlyList<Directory> directories)
        {
            var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (exifIfd0Directory != null)
            {
                var creditTag = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagCopyright);
                if (creditTag != null && creditTag.Description.HasValue())
                    return creditTag.Description;
            }

            var iptcDirectory = directories.OfType<IptcDirectory>().FirstOrDefault();
            if (iptcDirectory != null)
            {
                var creditTag = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagCredit);
                if (creditTag != null && creditTag.Description.HasValue())
                    return creditTag.Description;
            }

            var xmpDirectory = directories.OfType<XmpDirectory>().FirstOrDefault();
            if (xmpDirectory?.XmpMeta != null)
            {
                var creator = xmpDirectory.XmpMeta.Properties.SingleOrDefault(q => q.Path != null && q.Path.Equals("dc:creator[1]", StringComparison.CurrentCultureIgnoreCase) && !string.IsNullOrEmpty(q.Value));
                if (creator != null)
                    return creator.Value;

                var rights = xmpDirectory.XmpMeta.Properties.SingleOrDefault(q => q.Path != null && q.Path.Equals("dc:rights[1]", StringComparison.CurrentCultureIgnoreCase) && !string.IsNullOrEmpty(q.Value));
                if (rights != null)
                    return rights.Value;
            }

            return null;
        }
        #endregion
    }
}
