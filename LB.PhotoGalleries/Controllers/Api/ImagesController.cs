using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models.Exceptions;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers.Api;

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
    public async Task<ActionResult> SetPosition(string galleryId, string imageId, int position)
    {
        // Verify the user is authorized to edit images in this gallery
        var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
        if (image == null)
            return NotFound("Image not found");

        var gallery = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, galleryId);
        if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
            return Unauthorized("You are not authorised to modify images in this gallery.");

        await Server.Instance.Images.UpdateImagePositionAsync(galleryId, imageId, position);
        return Ok();
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

        var name = Request.Form["bulkName"].FirstOrDefault();

        var credit = Request.Form["bulkCredit"].FirstOrDefault();
        bool.TryParse(Request.Form["bulkCreditOnlyAddWhenMissing"].FirstOrDefault(), out var creditOnlyAddWhenMissing);

        var tagsCsv = Request.Form["bulkTags"].FirstOrDefault();
        if (!name.HasValue() && !credit.HasValue() && !tagsCsv.HasValue())
            return BadRequest("neither name, credit or tags supplied.");

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

            if (name.HasValue() && image.Name != name)
            {
                updateRequired = true;
                image.Name = name;
            }

            if (credit.HasValue())
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

    #region comments
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

        await Server.Instance.Images.DeleteCommentAsync(gallery, image, comment);
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
    #endregion

    #region tags
    [HttpPost("/api/images/add-tag")]
    [Authorize(Roles = "Administrator,Photographer")]
    public async Task<ActionResult> AddTag(string galleryId, string imageId, string tag)
    {
        var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
        if (image == null)
            return NotFound("Image not found");

        // Verify the user is authorized to edit images in this gallery
        var gallery = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, galleryId);
        if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
            return Unauthorized("You are not authorized to modify images in this gallery.");

        if (image.TagsCsv.TagsContain(tag))
            return Ok();

        image.TagsCsv = Utilities.AddTagToCsv(image.TagsCsv, tag);
        await Server.Instance.Images.UpdateImageAsync(image);
        return Ok();
    }

    [HttpPost("/api/images/add-tags")]
    [Authorize(Roles = "Administrator,Photographer")]
    public async Task<ActionResult> AddTags(string galleryId, string imageId, string tags)
    {
        if (!tags.HasValue())
            return BadRequest("tags is empty");

        if (!tags.Contains(','))
            return BadRequest("tags does not contain multiple items");

        var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
        if (image == null)
            return NotFound("Image not found");

        // Verify the user is authorized to edit images in this gallery
        var gallery = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, galleryId);
        if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
            return Unauthorized("You are not authorized to modify images in this gallery.");

        foreach (var tag in tags.Split(','))
        {
            var processedTag = tag.Trim().ToLower();
            if (image.TagsCsv.TagsContain(processedTag))
                continue;

            image.TagsCsv = Utilities.AddTagToCsv(image.TagsCsv, processedTag);
        }

        await Server.Instance.Images.UpdateImageAsync(image);
        return Ok();
    }

    [HttpDelete("/api/images/remove-tag")]
    [Authorize(Roles = "Administrator,Photographer")]
    public async Task<ActionResult> RemoveTag(string galleryId, string imageId, string tag)
    {
        var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
        if (image == null)
            return NotFound("Image not found");

        // Verify the user is authorized to edit images in this gallery
        var gallery = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, galleryId);
        if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
            return Unauthorized("You are not authorized to modify images in this gallery.");

        if (!image.TagsCsv.TagsContain(tag))
            return Ok();

        image.TagsCsv = Utilities.RemoveTagFromCsv(image.TagsCsv, tag);
        await Server.Instance.Images.UpdateImageAsync(image);
        return Ok();
    }
    #endregion

    [HttpPost("/api/images/replace-image")]
    [Authorize(Roles = "Administrator,Photographer")]
    [RequestSizeLimit(104857600)]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    public async Task<ActionResult> ReplaceImage(string galleryCategoryId, string galleryId, string imageId)
    {
        if (string.IsNullOrEmpty(galleryCategoryId))
            return BadRequest("galleryCategoryId value missing");

        if (string.IsNullOrEmpty(galleryId))
            return BadRequest("galleryId value missing");

        if (string.IsNullOrEmpty(imageId))
            return BadRequest("imageId value missing");

        // is the user authorised to edit this image?
        var gallery = await Server.Instance.Galleries.GetGalleryAsync(galleryCategoryId, galleryId);
        if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
            return Unauthorized("You are not authorised to replace that image.");

        // RequestSizeLimit: 104857600 = 100MB
        // store the file in cloud storage and post-process
        // follow secure uploads advice from: https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-3.1

        if (Request.Form.Files.Count == 0)
            return NoContent();

        var file = Request.Form.Files[0];
        if (!Server.Instance.Images.AcceptedContentTypes.Contains(file.ContentType))
            return BadRequest($"Sorry, content type of {file.ContentType} is not accepted.");

        var stream = file.OpenReadStream();

        try
        {
            await Server.Instance.Images.ReplaceImageAsync(galleryCategoryId, galleryId, imageId, stream, file.FileName);
        }
        catch (ImageTooSmallException e)
        {
            return BadRequest(e.Message);
        }
        finally
        {
            if (stream != null)
                await stream.DisposeAsync();
        }

        return Ok();
    }

    [Authorize(Roles = "Administrator")]
    [HttpPut("/api/images/reprocess-metadata")]
    public async Task<ActionResult> ReprocessMetadata(string galleryId, string imageId)
    {
        if (!galleryId.HasValue())
            return BadRequest("galleryId is empty!");

        if (!imageId.HasValue())
            return BadRequest("imageId is empty!");

        var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
        if (image == null)
            return BadRequest("Image not found");

        await Server.Instance.Images.ReprocessImageMetadataAsync(image);
        return Ok();
    }
}