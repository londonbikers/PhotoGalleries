using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrator,Photographer")]
    public class GalleriesController : Controller
    {
        // GET: /admin/galleries
        public async Task<ActionResult> Index()
        {
            ViewData.Model = await Server.Instance.Galleries.GetLatestGalleriesAsync(50);
            return View();
        }

        // GET: /admin/galleries/create
        public ActionResult Create()
        {
            return View();
        }

        // POST: /admin/galleries/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(Gallery gallery)
        {
            try
            {
                gallery.Id = Utilities.CreateNewId();
                gallery.CreatedByUserId = Utilities.GetUserId(User);
                await Server.Instance.Galleries.CreateOrUpdateGalleryAsync(gallery);
                return RedirectToAction(nameof(Edit), new { pk = gallery.CategoryId, id = gallery.Id });
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
                return View();
            }
        }

        // GET: /admin/galleries/edit/5/6
        public async Task<ActionResult> Edit(string pk, string id)
        {
            
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);
            var createdByUser = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
            ViewData.Model = gallery;
            ViewData["images"] = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            ViewData["username"] = createdByUser.Name;
            return View();
        }

        // POST: /admin/galleries/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string pk, string id, Gallery gallery)
        {
            var appGallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);
            var createdByUser = await Server.Instance.Users.GetUserAsync(appGallery.CreatedByUserId);
            var images = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);

            try
            {
                // map attributes from form gallery to one retrieved from the app server
                appGallery.Name = gallery.Name;
                appGallery.Description = gallery.Description;
                appGallery.Active = gallery.Active;
             
                await Server.Instance.Galleries.CreateOrUpdateGalleryAsync(appGallery);
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
            }

            ViewData.Model = appGallery;
            ViewData["username"] = createdByUser.Name;
            ViewData["images"] = images;

            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> Upload(string id, IFormFile file)
        {
            // store the file in cloud storage and post-process
            // follow secure uploads advice from: https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-3.1

            if (file.Length == 0)
                return NoContent();

            var stream = file.OpenReadStream();
            await Server.Instance.Images.CreateImageAsync(id, stream, file.FileName);
            return Ok();
        }

        // GET: /admin/galleries/delete/5
        public ActionResult Delete(string pk, string id)
        {
            return View();
        }

        // POST: /admin/galleries/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string pk, string id, IFormCollection collection)
        {
            try
            {
                // todo: ensure deletion can only be performed by administrators or creators of the gallery
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
