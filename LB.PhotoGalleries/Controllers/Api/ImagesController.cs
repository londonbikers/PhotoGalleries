using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [HttpPost("/api/images/SetPosition")]
        [Authorize(Roles = "Administrator,Photographer")]
        public async Task SetPosition(string galleryId, string imageId, int position)
        {
            // todo: check that the user is authorised to edit this image once caching is in place and such checks are cheap to perform
            await Server.Instance.Images.UpdateImagePositionAsync(galleryId, imageId, position);
        }

        [Authorize]
        [HttpPost("/api/images/comments")]
        public async Task<ActionResult> CreateComment(string galleryId, string imageId)
        {
            var comment = Request.Form["comment"];
            if (string.IsNullOrEmpty(comment) || string.IsNullOrWhiteSpace(comment))
                return BadRequest("comment missing");

            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var imageComment = new Comment
            {
                CreatedByUserId = Utilities.GetUserId(User),
                Text = comment
            };

            image.Comments.Add(imageComment);
            await Server.Instance.Images.UpdateImageAsync(image);
            return Accepted();
        }
    }
}
