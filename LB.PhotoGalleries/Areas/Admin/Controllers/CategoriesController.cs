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

        // GET: /admin/categories/details/5
        public ActionResult Details(string id)
        {
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
        public ActionResult Delete(string id)
        {
            ViewData.Model = Server.Instance.Categories.Categories.SingleOrDefault(q => q.Id.Equals(id));
            if (ViewData.Model == null)
                ViewData["error"] = "No category found with that id, sorry.";

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
