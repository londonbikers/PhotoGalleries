using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers.Api
{
    [Authorize(Roles = "Administrator,Photographer")]
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
        public async Task SetPosition(string galleryId, string imageId, int position)
        {
            // todo: check that the user is authorised to edit this image once caching is in place and such checks are cheap to perform
            await Server.Instance.Images.UpdateImagePositionAsync(galleryId, imageId, position);
        }
    }
}
