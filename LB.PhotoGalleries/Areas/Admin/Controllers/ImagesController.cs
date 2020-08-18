using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LB.PhotoGalleries.Areas.Admin.Controllers
{
    public class ImagesController : Controller
    {
        // GET: /admin/images
        public ActionResult Index()
        {
            return View();
        }

        // GET: /admin/images/details/5
        public ActionResult Details(string id)
        {
            return View();
        }


        // GET: /admin/images/edit/5
        public ActionResult Edit(string id)
        {
            return View();
        }

        // POST: /admin/images/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: /admin/images/delete/5
        public ActionResult Delete(string id)
        {
            return View();
        }

        // POST: /admin/images/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
