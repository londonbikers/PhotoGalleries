using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LB.PhotoGalleries.Controllers
{
    public class GalleriesController : Controller
    {
        #region members
        private readonly IConfiguration _configuration;
        #endregion

        #region constructors
        public GalleriesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        #endregion

        // GET: /g/<category>/<galleryId>/<name>
        public async Task<ActionResult> Details(string categoryName, string galleryId, string name)
        {
            ViewData["Configuration"] = _configuration;
            var decodedCategoryName = Helpers.DecodeParameterFromUrl(categoryName);
            var category = Server.Instance.Categories.Categories.SingleOrDefault(c => c.Name.Equals(decodedCategoryName, StringComparison.CurrentCultureIgnoreCase));
            if (category == null)
                return RedirectToAction("Index");

            var gallery = await Server.Instance.Galleries.GetGalleryAsync(category.Id, galleryId);
            if (gallery == null)
                return RedirectToAction("Index");
            
            ViewData["images"] = Utilities.OrderImages(await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id)).ToList();

            if (gallery.CreatedByUserId.HasValue())
            {
                // migrated galleries won't have a user id
                ViewData["user"] = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
            }

            ViewData.Model = gallery;

            long pixels = 0;
            foreach (var image in (List<Image>) ViewData["images"])
                if (image.Metadata.Width.HasValue && image.Metadata.Height.HasValue)
                    pixels += image.Metadata.Width.Value * image.Metadata.Height.Value;

            var megapixels = Convert.ToInt32(pixels / 1000000);
            ViewData["megapixels"] = megapixels;
            return View();
        }
    }
}
