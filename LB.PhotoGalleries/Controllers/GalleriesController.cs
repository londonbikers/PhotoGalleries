using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Mvc;
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

            ViewData.Model = gallery;
            return View();
        }
    }
}
