using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        /// <summary>
        /// Used to confirm a new image position within a gallery when images are re-ordered.
        /// </summary>
        /// <param name="galleryId">The id for the gallery that contains the image being re-ordered.</param>
        /// <param name="imageId">The id of the image being re-ordered.</param>
        /// <param name="position">The new position of the image in the gallery.</param>
        /// <returns>Nothing, unless something goes wrong :)</returns>
        [HttpPost("/api/images/set-position")]
        [Authorize(Roles = "Administrator,Photographer")]
        public async Task SetPosition(string galleryId, string imageId, int position)
        {
            // todo: check that the user is authorised to edit this image once caching is in place and such checks are cheap to perform
            await Server.Instance.Images.UpdateImagePositionAsync(galleryId, imageId, position);
        }

        /// <summary>
        /// Enable admins to bulk update credit and tags for all images in a gallery
        /// </summary>
        /// <param name="categoryId">The id for the category that the gallery resides in for these images.</param>
        /// <param name="galleryId">The id of the gallery to update each image for.</param>
        [HttpPost("/api/images/bulk-update")]
        [Authorize(Roles = "Administrator,Photographer")]
        public async Task<ActionResult> BulkUpdate(string categoryId, string galleryId)
        {
            if (string.IsNullOrEmpty(categoryId))
                return BadRequest("categoryId value missing");

            if (string.IsNullOrEmpty(galleryId))
                return BadRequest("galleryId value missing");

            // is the user authorised to edit these images?
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            if (!Utilities.CanUserEditObject(User, gallery.CreatedByUserId))
                return Unauthorized("You are not authorised to update these images.");

            var credit = Request.Form["credit"].FirstOrDefault();
            var tagsCsv = Request.Form["tags"].FirstOrDefault();
            if (string.IsNullOrEmpty(credit) && string.IsNullOrEmpty(tagsCsv))
                return BadRequest("neither credit or tags supplied.");

            string[] tags = null;
            if (!string.IsNullOrEmpty(tagsCsv))
            {
                tags = tagsCsv.Split(',');
                if (tags.Length == 0)
                    return BadRequest("tags doesn't contain comma-separated values.");
            }

            // update all images
            foreach (var image in await Server.Instance.Images.GetGalleryImagesAsync(galleryId))
            {
                if (!string.IsNullOrEmpty(credit))
                    image.Credit = credit;

                if (tags != null)
                {
                    // only add new tags, don't add duplicates
                    foreach (var tag in tags)
                        if (!image.Tags.Contains(tag))
                            image.Tags.Add(tag);
                }

                await Server.Instance.Images.UpdateImageAsync(image);
            }

            return Ok();
        }

        /// <summary>
        /// Let's a client tell the application that it's finished with a batch of image uploads.
        /// This allows the application to update gallery image counts and set any outstanding position values on images.
        /// </summary>
        /// <param name="categoryId">The id for the category that the gallery resides in for the uploaded images.</param>
        /// <param name="galleryId">The id of the gallery the images were uploaded for.</param>
        [HttpPost("/api/images/upload-complete")]
        [Authorize(Roles = "Administrator,Photographer")]
        public async Task<ActionResult> UploadComplete(string categoryId, string galleryId)
        {
            if (string.IsNullOrEmpty(categoryId))
                return BadRequest("categoryId value missing");

            if (string.IsNullOrEmpty(galleryId))
                return BadRequest("galleryId value missing");

            // is the user authorised to perform this operation?
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            if (!Utilities.CanUserEditObject(User, gallery.CreatedByUserId))
                return Unauthorized("You are not authorised to do mark that upload as complete.");

            // update image count
            await Server.Instance.Galleries.UpdateGalleryImageCount(categoryId, galleryId);

            // order images if necessary
            await Server.Instance.Images.UpdateImagePositionsAsync(galleryId);

            return Ok();
        }

        /// <summary>
        /// If we add new types of generated images to the app then new image files will need generating, this method will do that.
        /// </summary>
        /// <param name="galleryId">The unique identifier for the gallery to generate missing image files for.</param>
        /// <param name="imagePropertyName">The name of the property on an Image object that represents the image to generate, i.e. LowResStorageId</param>
        /// <returns>A set of responses for each image generated.</returns>
        [HttpPost("/api/images/generate-missing-image-files")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult> GenerateMissingImageFiles(string galleryId, string imagePropertyName)
        {
            if (string.IsNullOrEmpty(galleryId))
                return BadRequest("galleryId value missing");

            if (string.IsNullOrEmpty(imagePropertyName))
                return BadRequest("imagePropertyName value missing");

            var responses = await Server.Instance.Images.GenerateMissingImagesAsync(galleryId, imagePropertyName);
            return Ok(responses);
        }

        [Authorize]
        [HttpPost("/api/images/comments")]
        public async Task<ActionResult> CreateComment(string galleryId, string imageId)
        {
            var comment = Request.Form["comment"].FirstOrDefault();
            if (string.IsNullOrEmpty(comment) || string.IsNullOrWhiteSpace(comment))
                return BadRequest("comment missing");

            // todo: implement comment notifications...
            //var receiveNotifications = true; 
            //bool.TryParse(Request.Form["receiveNotifications"], out receiveNotifications);

            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var imageComment = new Comment
            {
                CreatedByUserId = Utilities.GetUserId(User),
                Text = comment.Trim()
            };

            image.Comments.Add(imageComment);
            await Server.Instance.Images.UpdateImageAsync(image);
            return Accepted();
        }

        [Authorize]
        [HttpDelete("/api/images/comments")]
        public async Task<ActionResult> DeleteComment(string categoryId, string galleryId, string imageId, long commentCreatedTicks, string commentCreatedByUserId)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var comment = image.Comments.SingleOrDefault(c => c.CreatedByUserId == commentCreatedByUserId && c.Created.Ticks == commentCreatedTicks);

            if (comment == null)
            {
                // comment doesn't exist. maybe it's already been deleted, either way, we're done.
                return NoContent();
            }

            if (!Utilities.CanUserEditComment(comment, gallery, User))
                return BadRequest("Apologies, you're not authorised to do that.");

            var removed = image.Comments.Remove(comment);
            if (removed)
                await Server.Instance.Images.UpdateImageAsync(image);
            else
                Debug.WriteLine($"ImageController.DeleteComment: Oops, no comment removed. galleryId={galleryId}, imageId={imageId}, commentCreatedTicks={commentCreatedTicks}, commentCreatedByUserId={commentCreatedByUserId}");

            return NoContent();
        }
    }
}
