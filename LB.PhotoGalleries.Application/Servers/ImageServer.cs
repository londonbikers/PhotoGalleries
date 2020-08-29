﻿using Azure.Storage.Blobs;
using LB.PhotoGalleries.Application.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Directory = MetadataExtractor.Directory;

namespace LB.PhotoGalleries.Application.Servers
{
    public class ImageServer
    {
        #region constructors
        internal ImageServer()
        {
        }
        #endregion

        #region public methods

        /// <summary>
        /// Stores an uploaded file in the storage system and adds a supporting Image object to the database.
        /// </summary>
        /// <param name="galleryCategoryId">The id for the category the gallery resides in, which the image resides in.</param>
        /// <param name="galleryId">The gallery this image is going to be contained within.</param>
        /// <param name="imageStream">The stream for the uploaded image file.</param>
        /// <param name="filename">The original filename provided by the client.</param>
        public async Task CreateImageAsync(string galleryCategoryId, string galleryId, Stream imageStream, string filename)
        {
            try
            {
                if (string.IsNullOrEmpty(galleryId))
                    throw new ArgumentNullException(nameof(galleryId));

                if (imageStream == null)
                    throw new ArgumentNullException(nameof(imageStream));

                if (string.IsNullOrEmpty(filename))
                    throw new ArgumentNullException(nameof(filename));

                // create the Image object
                // note: we don't set a position as there's no easy way to do this at this point. instead
                // we let the clients order by created date initially and then when/if a photographer orders the photos
                // then the position attribute is used to order images.
                var id = Guid.NewGuid().ToString();
                var image = new Image
                {
                    Id = id,
                    StorageId = id + Path.GetExtension(filename).ToLower(),
                    Name = Path.GetFileNameWithoutExtension(filename),
                    GalleryCategoryId = galleryCategoryId,
                    GalleryId = galleryId
                };

                ParseAndAssignImageMetadata(image, imageStream);

                if (!image.IsValid())
                    throw new InvalidOperationException("Image would be invalid. PLease check all required properties are set.");

                // upload the file to storage
                var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
                await blobContainerClient.UploadBlobAsync(image.StorageId, imageStream);
                imageStream.Close();

                // create the database record
                var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
                var response = await container.CreateItemAsync(image, new PartitionKey(image.GalleryId));
                Debug.WriteLine($"ImageServer.CreateImageAsync: Request charge: {response.RequestCharge}");

                // was this the first image? set the gallery thumbnail using this image if so
                var gallery = await Server.Instance.Galleries.GetGalleryAsync(galleryCategoryId, galleryId);
                if (string.IsNullOrEmpty(gallery.ThumbnailStorageId))
                {
                    gallery.ThumbnailStorageId = image.StorageId;
                    await Server.Instance.Galleries.CreateOrUpdateGalleryAsync(gallery);
                }
            }
            catch
            {
                // make sure we release valuable server resources in the event of a problem creating the image
                imageStream?.Close();
                throw;
            }
        }

        /// <summary>
        /// Updates an Image in the database. Must be provided with a complete and recently queried from the database image to avoid losing other recent updates.
        /// </summary>
        public async Task UpdateImageAsync(Image image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (!image.IsValid())
                throw new InvalidOperationException("Image is invalid. Please check all required properties are set.");

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var response = await container.ReplaceItemAsync(image, image.Id, new PartitionKey(image.GalleryId));
            Debug.WriteLine($"ImageServer.UpdateImageAsync: Request charge: {response.RequestCharge}");
        }

        public async Task<List<Image>> GetGalleryImagesAsync(string galleryId)
        {
            const string query = "SELECT * FROM c WHERE c.GalleryId = @galleryId";
            var queryDefinition = new QueryDefinition(query);
            queryDefinition.WithParameter("@galleryId", galleryId);

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = container.GetItemQueryIterator<Image>(queryDefinition);
            double charge = 0;
            var images = new List<Image>();

            while (queryResult.HasMoreResults)
            {
                var results = await queryResult.ReadNextAsync();
                images.AddRange(results);
                charge += results.RequestCharge;
            }

            Debug.WriteLine($"ImageServer.GetGalleryImagesAsync: Found {images.Count} gallery images");
            Debug.WriteLine($"ImageServer.GetGalleryImagesAsync: Total request charge: {charge}");

            return images;
        }

        public async Task<Image> GetImageAsync(string galleryId, string imageId)
        {
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var response = await container.ReadItemAsync<Image>(imageId, new PartitionKey(galleryId));
            Debug.WriteLine($"ImageServer:GetImageAsync: Request charge: {response.RequestCharge}");
            return response.Resource;
        }

        /// <summary>
        /// Causes an Image to be permanently deleted from storage and database. Will also result in some images being re-ordered to avoid holes in positions.
        /// </summary>
        public async Task DeleteImageAsync(Image image, bool performReordering = true)
        {
            // delete the original image from storage (any other resized caches will auto-expire and be deleted)
            var blobServiceClient = new BlobServiceClient(Server.Instance.Configuration["Storage:ConnectionString"]);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
            var blobResponse = await blobContainerClient.DeleteBlobAsync(image.StorageId);
            Debug.WriteLine($"ImageServer:DeleteImageAsync: Blob delete response: {blobResponse.Status} - {blobResponse.ReasonPhrase}");

            // make note of the image position and gallery id as we might have to re-order photos
            var position = image.Position;
            var galleryId = image.GalleryId;

            // finally, delete the database record
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var response = await container.DeleteItemAsync<Image>(image.Id, new PartitionKey(image.GalleryId));
            Debug.WriteLine($"ImageServer:DeleteImageAsync: Request charge: {response.RequestCharge}");

            // if necessary, re-order photos down-position from where the deleted photo used to be
            if (position.HasValue && performReordering)
            {
                var originalPosition = position.Value;
                Debug.WriteLine("ImageServer:DeleteImageAsync: Image had an order, re-ordering subsequent images...");

                // get the ids of images that have a position down from where our deleted image used to be
                var queryDefinition = new QueryDefinition("SELECT c.id AS Id, c.GalleryId AS PartitionKey FROM c WHERE c.GalleryId = @galleryId AND c.Position > @position ORDER BY c.Position")
                    .WithParameter("@galleryId", galleryId)
                    .WithParameter("@position", position.Value);

                var ids = await Server.Instance.Utilities.GetIdsByQueryAsync(Constants.GalleriesContainerName, queryDefinition);
                Image newThumbnailImage = null;

                for (var index = 0; index < ids.Count; index++)
                {
                    var id = ids[index].Id;
                    var affectedImage = await GetImageAsync(galleryId, id);

                    if (index == 0 && originalPosition == 0)
                        newThumbnailImage = affectedImage;

                    // this check shouldn't be required as if one image has a position then all should
                    // but life experience suggests it's best to be sure.
                    if (affectedImage.Position == null)
                        continue;

                    affectedImage.Position = position;
                    await UpdateImageAsync(affectedImage);
                    position += 1;
                }

                // update the gallery thumbnail if we deleted the first image
                if (originalPosition == 0 && newThumbnailImage != null)
                {
                    Debug.WriteLine($"ImageServer.DeleteImageAsync: New thumbnail image detected, updating gallery...");
                    var gallery = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, image.GalleryId);
                    gallery.ThumbnailStorageId = newThumbnailImage.StorageId;
                    await Server.Instance.Galleries.CreateOrUpdateGalleryAsync(gallery);
                }
            }
        }

        /// <summary>
        /// Re-orders gallery image positions so that an image is at a new position.
        /// </summary>
        public async Task UpdateImagePositionAsync(string galleryId, string imageId, int position)
        {
            var images = await GetGalleryImagesAsync(galleryId);

            // if images have not been ordered before, they will have no position set and the client
            // will be ordering images by when the images were created, so the first thing we need to do
            // is order the images like that, then re-order the images to what the user wishes.

            var hadToPerformInitialOrdering = false;
            if (!images.Any(i => i.Position.HasValue))
            {
                images = images.OrderBy(i => i.Created).ToList();
                for (var i = 0; i < images.Count; i++)
                    images[i].Position = i;

                hadToPerformInitialOrdering = true;
            }

            // now we can re-order the images
            // cut out the image being ordered first then bump up a position each image from where our image will go
            var imageBeingOrdered = images.Single(i => i.Id == imageId);
            images.RemoveAll(i => i.Id == imageId);

            var newPosition = position + 1;
            foreach (var image in images.Where(image => image.Position >= position))
            {
                image.Position = newPosition;
                newPosition += 1;
            }

            // now set the desired image with the desired position and add it back in to the list
            imageBeingOrdered.Position = position;
            images.Add(imageBeingOrdered);

            // now write the new positions back to the database
            // we have an opportunity to introduce some efficiency, i.e. if we are moving an image from the back to the middle then
            // we don't need to re-order images at the start. this will save on db interactions.
            var imagesToUpdate = hadToPerformInitialOrdering ? images : images.Where(i => i.Position >= position);
            foreach (var image in imagesToUpdate)
                await UpdateImageAsync(image);

            if (position == 0)
            {
                Debug.WriteLine("ImageServer.UpdateImagePositionAsync: New position is 0, need to update gallery thumbnail...");
                var gallery = await Server.Instance.Galleries.GetGalleryAsync(imageBeingOrdered.GalleryCategoryId, imageBeingOrdered.GalleryId);
                gallery.ThumbnailStorageId = imageBeingOrdered.StorageId;
                await Server.Instance.Galleries.CreateOrUpdateGalleryAsync(gallery);
            }
        }
        #endregion

        #region internal methods
        internal async Task<List<Image>> GetImagesUserHasCommentedOnAsync(string userId)
        {
            const string query = "SELECT * FROM c WHERE c.Comments.CreatedByUserId = @userId";
            var queryDefinition = new QueryDefinition(query).WithParameter("@userId", userId);
            return await GetImagesByQueryAsync(queryDefinition);
        }

        internal async Task<int> GetImagesScalarByQueryAsync(QueryDefinition queryDefinition)
        {
            if (queryDefinition == null)
                throw new InvalidOperationException("queryDefinition is null");

            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var result = container.GetItemQueryIterator<object>(queryDefinition);

            const int count = 0;
            if (!result.HasMoreResults)
                return count;

            var resultSet = await result.ReadNextAsync();
            Debug.WriteLine($"ImageServer.GetImagesScalarByQueryAsync: Query: {queryDefinition.QueryText}");
            Debug.WriteLine($"ImageServer.GetImagesScalarByQueryAsync: Request charge: {resultSet.RequestCharge}");

            return Convert.ToInt32(resultSet.Resource.First());
        }
        #endregion

        #region private methods
        private static async Task<List<Image>> GetImagesByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.ImagesContainerName);
            var queryResult = container.GetItemQueryIterator<Image>(queryDefinition);
            var users = new List<Image>();
            double charge = 0;

            while (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                charge += resultSet.RequestCharge;
                users.AddRange(resultSet);
            }

            Debug.WriteLine($"ImageServer.GetImagesByQueryAsync: Query: {queryDefinition.QueryText}");
            Debug.WriteLine($"ImageServer.GetImagesByQueryAsync: Total request charge: {charge}");

            return users;
        }
        #endregion

        #region metadata parsing methods
        /// <summary>
        /// Images contain metadata that describes the photo to varying degrees. This method extracts the metadata
        /// and parses out the most interesting pieces we're interested in and assigns it to the image object so we can
        /// present the information to the user and use it to help with searches.
        /// </summary>
        /// <param name="image">The Image object to assign the metadata to.</param>
        /// <param name="imageStream">The stream containing the recently-uploaded image file to inspect for metadata.</param>
        private static void ParseAndAssignImageMetadata(Image image, Stream imageStream)
        {
            var directories = ImageMetadataReader.ReadMetadata(imageStream);

            // debug info
            // ----------------------------------------------------------------------------------
            //Debug.WriteLine("-------------------------------------------------");
            //Debug.WriteLine("");
            //foreach (var directory in directories)
            //{
            //    // Each directory stores values in tags
            //    foreach (var tag in directory.Tags)
            //        Debug.WriteLine(tag);

            //    // Each directory may also contain error messages
            //    foreach (var error in directory.Errors)
            //        Debug.WriteLine("ERROR: " + error);
            //}
            //Debug.WriteLine("");
            //Debug.WriteLine("-------------------------------------------------");
            // ----------------------------------------------------------------------------------

            image.Metadata.TakenDate = GetImageDateTaken(directories);

            var size = GetImageDimensions(directories);
            if (size.HasValue && !size.Value.IsEmpty)
            {
                image.Metadata.Width = size.Value.Width;
                image.Metadata.Height = size.Value.Height;
            }

            var iso = GetImageIso(directories);
            if (iso.HasValue)
                image.Metadata.Iso = iso.Value;

            var credit = GetImageCredit(directories);
            if (credit.HasValue())
                image.Credit = credit;

            var exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (exifIfd0Directory != null)
            {
                var make = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagMake);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (make != null && make.Description.HasValue())
                    image.Metadata.CameraMake = make.Description;

                var model = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagModel);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (model != null && model.Description.HasValue())
                    image.Metadata.CameraModel = model.Description;

                var imageDescription = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagImageDescription);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (imageDescription != null && imageDescription.Description.HasValue())
                    image.Caption = imageDescription.Description;
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
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (objectName != null && objectName.Description.HasValue())
                    image.Name = objectName.Description;

                var keywords = iptcDirectory.GetKeywords();
                if (keywords != null)
                {
                    foreach (var keyword in keywords)
                    {
                        // make sure we don't add duplicate keywords
                        if (keyword.HasValue() && !image.Tags.Any(t => t.Equals(keyword, StringComparison.CurrentCultureIgnoreCase)))
                            image.Tags.Add(keyword.ToLower());
                    }
                }
            }

            // wind the stream back to allow other code to work with the stream
            imageStream.Position = 0;
        }

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
        /// Attempts to extract image width and height information from image metadata.
        /// </summary>
        /// <param name="directories">Metadata directories extracted from the image stream.</param>
        public static Size? GetImageDimensions(IEnumerable<Directory> directories)
        {
            var size = new Size();

            // try and get dimensions from EXIF data first
            var enumerable = directories as Directory[] ?? directories.ToArray();
            var exifSubIfdDirectory = enumerable.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfdDirectory != null)
            {
                var width = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagExifImageWidth);
                if (width != null && width.Description.HasValue())
                {
                    // values can contain strings, i.e. "1024 pixels" so snip those off
                    var spacePosition = width.Description.IndexOf(" ", StringComparison.Ordinal);
                    size.Width = Convert.ToInt32(spacePosition != -1 ? width.Description.Substring(0, spacePosition) : width.Description);
                }

                var height = exifSubIfdDirectory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagExifImageHeight);
                if (height != null && height.Description.HasValue())
                {
                    // values can contain strings, i.e. "768 pixels" so snip those off
                    var spacePosition = height.Description.IndexOf(" ", StringComparison.Ordinal);
                    size.Height = Convert.ToInt32(spacePosition != -1 ? height.Description.Substring(0, spacePosition) : height.Description);
                }

                if (!size.IsEmpty)
                    return size;
            }

            // no luck, try the JPEG data next
            var jpegDirectory = enumerable.OfType<JpegDirectory>().FirstOrDefault();
            if (jpegDirectory != null)
            {
                var width = jpegDirectory.Tags.SingleOrDefault(t => t.Type == JpegDirectory.TagImageWidth);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (width != null && width.Description.HasValue())
                {
                    // values can contain strings, i.e. "1024 pixels" so snip those off
                    var spacePosition = width.Description.IndexOf(" ", StringComparison.Ordinal);
                    size.Width = Convert.ToInt32(spacePosition != -1 ? width.Description.Substring(0, spacePosition) : width.Description);
                }

                var height = jpegDirectory.Tags.SingleOrDefault(t => t.Type == JpegDirectory.TagImageHeight);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (height != null && height.Description.HasValue())
                {
                    // values can contain strings, i.e. "768 pixels" so snip those off
                    var spacePosition = height.Description.IndexOf(" ", StringComparison.Ordinal);
                    size.Height = Convert.ToInt32(spacePosition != -1 ? height.Description.Substring(0, spacePosition) : height.Description);
                }

                return size;
            }

            // no luck, try the PNG data next
            var pngDirectory = enumerable.OfType<PngDirectory>().FirstOrDefault();
            if (pngDirectory != null)
            {
                var width = pngDirectory.Tags.SingleOrDefault(t => t.Type == PngDirectory.TagImageWidth);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (width != null && !string.IsNullOrEmpty(width.Description))
                {
                    // values can contain strings, i.e. "1024 pixels" so snip those off
                    var spacePosition = width.Description.IndexOf(" ", StringComparison.Ordinal);
                    size.Width = Convert.ToInt32(spacePosition != -1 ? width.Description.Substring(0, spacePosition) : width.Description);
                }

                var height = pngDirectory.Tags.SingleOrDefault(t => t.Type == PngDirectory.TagImageHeight);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (height != null && !string.IsNullOrEmpty(height.Description))
                {
                    // values can contain strings, i.e. "768 pixels" so snip those off
                    var spacePosition = height.Description.IndexOf(" ", StringComparison.Ordinal);
                    size.Height = Convert.ToInt32(spacePosition != -1 ? height.Description.Substring(0, spacePosition) : height.Description);
                }

                return size;
            }

            return null;
        }

        /// <summary>
        /// Attempts to extract image iso information from image metadata.
        /// </summary>
        /// <param name="directories">Metadata directories extracted from the image stream.</param>
        public static int? GetImageIso(IEnumerable<Directory> directories)
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

                    Debug.WriteLine($"ImageServer.GetImageIso: ExifSubIfdDirectory iso tag value wasn't an int: '{isoTag.Description}'");
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

                var isoTagProcessed = isoTag.Description;
                if (isoTagProcessed.StartsWith("ISO "))
                    isoTagProcessed = isoTag.Description.Split(' ')[1];

                var validIso = int.TryParse(isoTagProcessed, out iso);
                if (validIso)
                    return iso;

                Debug.WriteLine($"ImageServer.GetImageIso: NikonType2MakernoteDirectory iso tag value wasn't an int: '{isoTag.Description}'");
            }

            return null;
        }

        /// <summary>
        /// Attempts to extract image credit information from image metadata.
        /// </summary>
        /// <param name="directories">Metadata directories extracted from the image stream.</param>
        public static string GetImageCredit(IEnumerable<Directory> directories)
        {
            var enumerable = directories as Directory[] ?? directories.ToArray();
            var exifIfd0Directory = enumerable.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (exifIfd0Directory != null)
            {
                var creditTag = exifIfd0Directory.Tags.SingleOrDefault(t => t.Type == ExifDirectoryBase.TagCopyright);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (creditTag != null && creditTag.Description.HasValue())
                    return creditTag.Description;
            }

            var iptcDirectory = enumerable.OfType<IptcDirectory>().FirstOrDefault();
            if (iptcDirectory != null)
            {
                var creditTag = iptcDirectory.Tags.SingleOrDefault(t => t.Type == IptcDirectory.TagCredit);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse -- wrong, can return null
                if (creditTag != null && creditTag.Description.HasValue())
                    return creditTag.Description;
            }

            return null;
        }
        #endregion
    }
}
