using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrator")]
    public class CategoriesController : Controller
    {
        // GET: /admin/categories
        public ActionResult Index()
        {
            ViewData.Model = Server.Instance.Categories.Categories;
            return View();
        }

        // GET: /admin/categories/create
        public ActionResult Create()
        {
            return View();
        }

        // POST: /admin/categories/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(Category category)
        {
            try
            {
                await Server.Instance.Categories.CreateOrUpdateCategoryAsync(category);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
                return View();
            }
        }

        // GET: /admin/categories/edit/5
        public ActionResult Edit(string id)
        {
            ViewData.Model = Server.Instance.Categories.Categories.SingleOrDefault(q => q.Id.Equals(id));
            if (ViewData.Model == null)
                ViewData["error"] = "No category found with that id, sorry.";

            return View();
        }

        // POST: /admin/categories/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string id, Category category)
        {
            try
            {
                await Server.Instance.Categories.CreateOrUpdateCategoryAsync(category);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
                return View();
            }
        }

        // GET: /admin/categories/delete/5
        public async Task<ActionResult> Delete(string id)
        {
            var category = Server.Instance.Categories.Categories.SingleOrDefault(q => q.Id.Equals(id));
            if (category == null)
            {
                ViewData["error"] = "No category found with that id, sorry.";
            }
            else
            {
                var galleryCount = await Server.Instance.Categories.GetCategoryGalleryCountAsync(category);
                ViewData.Model = category;
                ViewData["galleryCount"] = galleryCount;
            }
            
            return View();
        }

        // POST: /admin/categories/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
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
