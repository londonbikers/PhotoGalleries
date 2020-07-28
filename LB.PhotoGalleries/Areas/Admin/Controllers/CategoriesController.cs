﻿using System;
using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            ViewData["categories"] = Server.Instance.Categories.Categories;
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
            return View();
        }

        // POST: /admin/categories/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
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

        // GET: /admin/categories/delete/5
        public ActionResult Delete(int id)
        {
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
