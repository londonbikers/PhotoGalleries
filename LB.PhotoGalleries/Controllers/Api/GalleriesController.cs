using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class GalleriesController : ControllerBase
    {
        /// <summary>
        /// Used to confirm a new image position within a gallery when images are re-ordered.
        /// </summary>
        /// <param name="categoryId">The id of the category the gallery resides in.</param>
        /// <param name="galleryId">The id for the gallery to have the image count updated on.</param>
        /// <returns>Nothing</returns>
        [HttpPost("/api/galleries/update-image-count")]
        [Authorize(Roles = "Administrator,Photographer")]
        public async Task<ActionResult> UpdateImageCount(string categoryId, string galleryId)
        {
            await Server.Instance.Galleries.UpdateGalleryImageCount(categoryId, galleryId);
            return Ok();
        }
    }
}
