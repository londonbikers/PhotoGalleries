using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Shared;
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
            if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
                return Unauthorized("You are not authorised to update these images.");

            var credit = Request.Form["bulkCredit"].FirstOrDefault();
            bool.TryParse(Request.Form["bulkCreditOnlyAddWhenMissing"].FirstOrDefault(), out var creditOnlyAddWhenMissing);
            var tagsCsv = Request.Form["bulkTags"].FirstOrDefault();
            if (string.IsNullOrEmpty(credit) && string.IsNullOrEmpty(tagsCsv))
                return BadRequest("neither credit or tags supplied.");

            string[] newTags = null;
            if (!string.IsNullOrEmpty(tagsCsv))
            {
                newTags = tagsCsv.Split(',');
                if (newTags.Length == 0)
                    return BadRequest("tags doesn't contain comma-separated values.");
            }

            // update all images
            foreach (var image in await Server.Instance.Images.GetGalleryImagesAsync(galleryId))
            {
                var updateRequired = false;
                if (!string.IsNullOrEmpty(credit))
                {
                    if (creditOnlyAddWhenMissing)
                    {
                        if (string.IsNullOrEmpty(image.Credit))
                        {
                            updateRequired = true;
                            image.Credit = credit;
                        }
                    }
                    else if (image.Credit != credit)
                    {
                        updateRequired = true;
                        image.Credit = credit;
                    }
                }

                if (newTags != null)
                {
                    foreach (var newTag in newTags)
                    {
                        // only add new tags, don't add duplicates
                        if (image.TagsCsv.TagsContain(newTag)) 
                            continue;

                        image.TagsCsv = Utilities.AddTagToCsv(image.TagsCsv, newTag);
                        updateRequired = true;
                    }
                }

                if (updateRequired)
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
            if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
                return Unauthorized("You are not authorised to do mark that upload as complete.");

            // update image count
            await Server.Instance.Galleries.UpdateGalleryImageCount(categoryId, galleryId);

            return Ok();
        }

        /// <summary>
        /// Causes image files to be regenerated for all images in a gallery.
        /// </summary>
        /// <param name="galleryId">The unique identifier for the gallery to generate image files for.</param>
        [HttpPost("/api/images/generate-image-files")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult> GenerateImageFiles(string galleryId)
        {
            if (string.IsNullOrEmpty(galleryId))
                return BadRequest("galleryId value missing");

            await Server.Instance.Images.RegenerateImageFiles(galleryId);
            return Ok();
        }

        [HttpPost("/api/images/delete-pregen-image-files")]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult> DeletePreGenImageFiles(string categoryId, string galleryId)
        {
            if (string.IsNullOrEmpty(categoryId))
                return BadRequest("categoryId value missing");

            if (string.IsNullOrEmpty(galleryId))
                return BadRequest("galleryId value missing");

            await Server.Instance.Images.DeletePreGenImageFilesAsync(categoryId, galleryId);
            return Ok();
        }

        [Authorize]
        [HttpPost("/api/images/comments")]
        public async Task<ActionResult> CreateComment(string galleryId, string imageId)
        {
            var comment = Request.Form["comment"].FirstOrDefault();
            if (string.IsNullOrEmpty(comment) || string.IsNullOrWhiteSpace(comment))
                return BadRequest("comment missing");

            var userId = Helpers.GetUserId(User);

            // subscribe the user to comment notifications if they've asked to be
            bool.TryParse(Request.Form["receiveNotifications"], out var receiveNotifications);
            
            await Server.Instance.Images.CreateCommentAsync(comment, userId, receiveNotifications, galleryId, imageId);
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

            if (!Helpers.CanUserEditComment(comment, gallery, User))
                return BadRequest("Apologies, you're not authorised to do that.");

            var removed = image.Comments.Remove(comment);
            if (removed)
                await Server.Instance.Images.UpdateImageAsync(image);
            else
                Debug.WriteLine($"ImageController.DeleteComment: Oops, no comment removed. galleryId={galleryId}, imageId={imageId}, commentCreatedTicks={commentCreatedTicks}, commentCreatedByUserId={commentCreatedByUserId}");

            return NoContent();
        }

        [Authorize]
        [HttpDelete("/api/images/comment-subscriptions")]
        public async Task<ActionResult> DeleteUserCommentSubscription(string galleryId, string imageId)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var userId = Helpers.GetUserId(User);
            if (image.UserCommentSubscriptions.Contains(userId))
            {
                image.UserCommentSubscriptions.Remove(userId);
                await Server.Instance.Images.UpdateImageAsync(image);
                return Accepted();
            }

            return NotFound();
        }

        [HttpPost("/api/images/add-tag")]
        [Authorize(Roles = "Administrator,Photographer")]
        public async Task<ActionResult> AddTag(string galleryId, string imageId, string tag)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);

            var list = Utilities.CsvToList(image.TagsCsv);
            if (list.Contains(tag))
                return BadRequest("Tag already exists on image");

            image.TagsCsv = Utilities.AddTagToCsv(image.TagsCsv, tag);
            await Server.Instance.Images.UpdateImageAsync(image);
            return Ok();

        }

        [HttpDelete("/api/images/remove-tag")]
        [Authorize(Roles = "Administrator,Photographer")]
        public async Task<ActionResult> RemoveTag(string galleryId, string imageId, string tag)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            if (!image.TagsCsv.TagsContain(tag)) 
                return BadRequest("Tag does not exist on image");

            image.TagsCsv = Utilities.RemoveTagFromCsv(image.TagsCsv, tag);
            await Server.Instance.Images.UpdateImageAsync(image);
            return Ok();

        }
    }
}
