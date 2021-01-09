using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class ImagesController : Controller
    {
        #region members
        private readonly IConfiguration _configuration;
        #endregion

        #region constructors
        public ImagesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        #endregion

        // GET: /gi/{galleryId}/{imageId}/{name}
        public async Task<ActionResult> Details(string galleryId, string imageId, string name)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            if (image == null)
                return RedirectToActionPermanent("Index", "Home");

            ViewData.Model = image;
            ViewData["gallery"] = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, galleryId);
            ViewData["category"] = Server.Instance.Categories.Categories.Single(c => c.Id == image.GalleryCategoryId);
            ViewData["mapsKey"] = _configuration["Google:MapsApiKey"];

            // work out the previous image in the gallery for navigation purposes
            ViewData["previousImage"] = await Server.Instance.Images.GetPreviousImageInGalleryAsync(image);
            ViewData["nextImage"] = await Server.Instance.Images.GetNextImageInGalleryAsync(image);

            // record the image view if the user hasn't viewed the image before in their current session
            var viewedImages = HttpContext.Session.Get<List<string>>("viewedImages") ?? new List<string>();
            if (!viewedImages.Contains(image.Id))
            {
                await Server.Instance.Images.IncreaseImageViewsAsync(galleryId, imageId);
                viewedImages.Add(image.Id);
                HttpContext.Session.Set("viewedImages", viewedImages);
            }

            return View();
        }
    }
}
