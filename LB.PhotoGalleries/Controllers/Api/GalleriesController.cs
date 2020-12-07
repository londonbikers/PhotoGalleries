using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
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
    public class GalleriesController : ControllerBase
    {
        [Authorize]
        [HttpPost("/api/galleries/comments")]
        public async Task<ActionResult> CreateComment(string categoryId, string galleryId)
        {
            var comment = Request.Form["comment"].FirstOrDefault();
            if (string.IsNullOrEmpty(comment) || string.IsNullOrWhiteSpace(comment))
                return BadRequest("comment missing");

            // todo: implement comment notifications...
            //var receiveNotifications = true; 
            //bool.TryParse(Request.Form["receiveNotifications"], out receiveNotifications);

            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            var galleryComment = new Comment
            {
                CreatedByUserId = Helpers.GetUserId(User),
                Text = comment.Trim()
            };

            gallery.Comments.Add(galleryComment);
            await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
            return Ok();
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

            var removed = gallery.Comments.Remove(comment);
            if (removed)
                await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
            else
                Debug.WriteLine($"Api:GalleriesController.DeleteComment: Oops, no comment removed. galleryId={galleryId}, commentCreatedTicks={commentCreatedTicks}, commentCreatedByUserId={commentCreatedByUserId}");

            return NoContent();
        }

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
    }
}
