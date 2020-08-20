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
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData.Model = image;
            ViewData["gallery"] = gallery;
            ViewData["tags"] = string.Join(',', image.Tags);
            ViewData["isAuthorisedToEdit"] = User.IsInRole("Administrator") || gallery.CreatedByUserId == Utilities.GetUserId(User);
            return View();
        }

        // POST: /admin/images/edit/5/6/7
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string categoryId, string galleryId, string imageId, IFormCollection collection)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData.Model = image;
            ViewData["gallery"] = gallery;
            ViewData["isAuthorisedToEdit"] = User.IsInRole("Administrator") || gallery.CreatedByUserId == Utilities.GetUserId(User);

            try
            {
                // check that the user is authorised to edit the image, i.e. they're an administrator or the creator of the gallery
                if (!User.IsInRole("Administrator") && gallery.CreatedByUserId != Utilities.GetUserId(User))
                {
                    ViewData["error"] = "Sorry, you are not authorised to edit this image. You did not create the gallery.";
                    return View();
                }

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
        public async Task<ActionResult> Delete(string categoryId, string galleryId, string imageId)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            ViewData.Model = image;
            ViewData["gallery"] = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            return View();
        }

        // POST: /admin/images/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(string categoryId, string galleryId, string imageId, IFormCollection collection)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData.Model = image;
            ViewData["gallery"] = gallery;

            try
            {
                // check that the user is authorised to delete the image, i.e. they're an administrator or the creator of the gallery
                if (!User.IsInRole("Administrator") && gallery.CreatedByUserId != Utilities.GetUserId(User))
                {
                    ViewData["error"] = "Sorry, you are not authorised to delete this image. You did not create this gallery.";
                    return View();
                }

                await Server.Instance.Images.DeleteImageAsync(image);
                return RedirectToAction("Edit", "Galleries", new { pk = categoryId, id = galleryId });
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
                return View();
            }
        }
    }
}
