using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace LB.PhotoGalleries.Controllers
{
    public class CategoriesController : Controller
    {
        // GET: /categories
        public ActionResult Index()
        {
            return View();
        }

        // GET: /categories/motorcycles
        public ActionResult Details(string name)
        {
            name = Utilities.DecodeParameterFromUrl(name);
            var category = Server.Instance.Categories.Categories.SingleOrDefault(c => c.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (category == null)
                return RedirectToAction("Index", "Home");

            ViewData.Model = category;
            return View();
        }
    }
}
