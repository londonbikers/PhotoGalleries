using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class GalleriesController : Controller
    {
        // GET: /g
        public ActionResult Index()
        {
            return View();
        }

        // GET: /g/<categoryId>/<galleryId>/<name>
        public async Task<ActionResult> Details(string categoryId, string galleryId, string name)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            if (gallery == null)
                return RedirectToAction("Index");
            
            ViewData["images"] = Utilities.OrderImages(await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id)).ToList();
            ViewData["user"] = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
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
