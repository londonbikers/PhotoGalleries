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
        // GET: /admin/images/edit/5/5/7
        public async Task<ActionResult> Edit(string categoryId, string galleryId, string imageId)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            ViewData.Model = image;
            ViewData["gallery"] = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData["tags"] = string.Join(',', image.Tags);
            return View();
        }

        // POST: /admin/images/edit/5/6/7
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string categoryId, string galleryId, string imageId, IFormCollection collection)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            ViewData.Model = image;
            ViewData["gallery"] = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);

            try
            {
                image.Name = collection["Name"];
                image.Caption = collection["Caption"];
                image.Tags.Clear();
                image.Tags.AddRange(collection["tagsCsv"].ToString().Split(','));
                ViewData["tags"] = string.Join(',', image.Tags);
                await Server.Instance.Images.UpdateImageAsync(image);
                ViewData["success"] = "Image updated!";
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
            }

            return View();
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
