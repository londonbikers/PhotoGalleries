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
                await Server.Instance.Galleries.CreateOrUpdateGalleryAsync(gallery);
                return RedirectToAction(nameof(Edit), new { gallery.Id });
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
                return View();
            }
        }

        // GET: /admin/galleries/edit/5
        public ActionResult Edit(string id)
        {
            return View();
        }

        // POST: /admin/galleries/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, Gallery gallery)
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

        // GET: /admin/galleries/delete/5
        public ActionResult Delete(string id)
        {
            return View();
        }

        // POST: /admin/galleries/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id, IFormCollection collection)
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
