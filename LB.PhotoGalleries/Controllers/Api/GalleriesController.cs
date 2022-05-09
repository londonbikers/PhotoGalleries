using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class GalleriesController : ControllerBase
    {
        #region comments
        [Authorize]
        [HttpPost("/api/galleries/comments")]
        public async Task<ActionResult> CreateComment(string categoryId, string galleryId)
        {
            var comment = Request.Form["comment"].FirstOrDefault();
            if (string.IsNullOrEmpty(comment) || string.IsNullOrWhiteSpace(comment))
                return BadRequest("comment missing");

            var userId = Helpers.GetUserId(User);

            // subscribe the user to comment notifications if they've asked to be
            bool.TryParse(Request.Form["receiveNotifications"], out var receiveNotifications);
            await Server.Instance.Galleries.CreateCommentAsync(comment, userId, receiveNotifications, categoryId, galleryId);
            return Accepted();
        }

        [Authorize]
        [HttpDelete("/api/galleries/comments")]
        public async Task<ActionResult> DeleteComment(string categoryId, string galleryId, long commentCreatedTicks, string commentCreatedByUserId)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            var comment = gallery.Comments.SingleOrDefault(c => c.CreatedByUserId == commentCreatedByUserId && c.Created.Ticks == commentCreatedTicks);

            if (comment == null)
            {
                // comment doesn't exist. maybe it's already been deleted, either way, we're done.
                return NoContent();
            }

            if (!Helpers.CanUserEditComment(comment, gallery, User))
                return BadRequest("Apologies, you're not authorised to do that.");

            await Server.Instance.Galleries.DeleteCommentAsync(gallery, comment);
            return NoContent();
        }

        [Authorize]
        [HttpDelete("/api/galleries/comment-subscriptions")]
        public async Task<ActionResult> DeleteUserCommentSubscription(string categoryId, string galleryId)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            var userId = Helpers.GetUserId(User);
            if (gallery.UserCommentSubscriptions.Contains(userId))
            {
                gallery.UserCommentSubscriptions.Remove(userId);
                await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
                return Accepted();
            }

            return NotFound();
        }
        #endregion

        #region tags
        [HttpDelete("/api/galleries/remove-tag")]
        [Authorize(Roles = "Administrator,Photographer")]
        public async Task<ActionResult> RemoveTag(string categoryId, string galleryId, string tag)
        {
            if (!tag.HasValue())
                return BadRequest("tag is empty!");

            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
                return BadRequest("Apologies, you're not authorised to do that.");

            tag = tag.ToLower();
            var images = await Server.Instance.Images.GetGalleryImagesAsync(galleryId);
            var tagImages = images.Where(i => i.TagsCsv.TagsContain(tag));

            foreach (var i in tagImages)
            {
                i.TagsCsv = Utilities.RemoveTagFromCsv(i.TagsCsv, tag);
                await Server.Instance.Images.UpdateImageAsync(i);
            }

            return Ok($"{tag} tag removed from all gallery images");
        }
        #endregion

        [Authorize(Roles = "Administrator,Photographer")]
        [HttpPut("/api/galleries/order-images")]
        public async Task<ActionResult> OrderImages(string categoryId, string galleryId, string by)
        {
            if (!categoryId.HasValue())
                return BadRequest("categoryId is empty!");
            if (!galleryId.HasValue())
                return BadRequest("galleryId is empty!");
            if (!by.HasValue())
                return BadRequest("by is empty!");

            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
                return BadRequest("Apologies, you're not authorised to do that.");

            OrderBy orderBy;
            try
            {
                orderBy = (OrderBy)Enum.Parse(typeof(OrderBy), by, true);
            }
            catch
            {
                return BadRequest("Invalid parameter 'by' value");
            }

            await Server.Instance.Galleries.OrderImagesAsync(gallery, orderBy);
            return Ok();
        }

        [Authorize(Roles = "Administrator,Photographer")]
        [HttpPut("/api/galleries/update-created")]
        public async Task<ActionResult> UpdateCreated(string categoryId, string galleryId, long created)
        {
            if (!categoryId.HasValue())
                return BadRequest("categoryId is empty!");
            if (!galleryId.HasValue())
                return BadRequest("galleryId is empty!");
            if (created <= 0)
                return BadRequest("created is empty!");

            var createdDate = new DateTime(created);
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
                return BadRequest("Apologies, you're not authorised to do that.");

            gallery.Created = createdDate;
            await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
            return Ok();
        }

        [Authorize(Roles = "Administrator")]
        [HttpPut("/api/galleries/reprocess-metadata")]
        public async Task<ActionResult> ReprocessMetadata(string galleryId)
        {
            if (!galleryId.HasValue())
                return BadRequest("galleryId is empty!");

            await Server.Instance.Galleries.ReprocessGalleryMetadataAsync(galleryId);
            return Ok();
        }
    }
}
