using System.Linq;
using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class ImagesController : Controller
    {
        // GET: /gi
        public ActionResult Index()
        {
            return View();
        }

        // GET: /gi/{galleryId}/{imageId}/{name}
        public async Task<ActionResult> Details(string galleryId, string imageId, string name)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            if (image == null)
                return RedirectToActionPermanent("Index", "Home");

            ViewData.Model = image;
            ViewData["gallery"] = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, galleryId);
            ViewData["category"] = Server.Instance.Categories.Categories.Single(c => c.Id == image.GalleryCategoryId);

            return View();
        }
    }
}
