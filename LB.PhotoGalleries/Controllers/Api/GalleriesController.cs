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
                CreatedByUserId = Utilities.GetUserId(User),
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

            if (!Utilities.CanUserEditComment(comment, gallery, User))
                return BadRequest("Apologies, you're not authorised to do that.");

            var removed = gallery.Comments.Remove(comment);
            if (removed)
                await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
            else
                Debug.WriteLine($"Api:GalleriesController.DeleteComment: Oops, no comment removed. galleryId={galleryId}, commentCreatedTicks={commentCreatedTicks}, commentCreatedByUserId={commentCreatedByUserId}");

            return NoContent();
        }
    }
}
