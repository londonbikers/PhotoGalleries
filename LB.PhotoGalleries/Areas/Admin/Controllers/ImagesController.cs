using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrator,Photographer")]
    public class ImagesController : Controller
    {
        // GET: /admin/images/edit/5
        public async Task<ActionResult> Edit(string categoryId, string galleryId, string imageId)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            ViewData.Model = image;
            ViewData["gallery"] = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData["tags"] = string.Join(',', image.Tags);
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
