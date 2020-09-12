using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class CategoriesController : Controller
    {
        // GET: /categories/motorcycles
        public async Task<ActionResult> Details(string name, int p = 1)
        {
            name = Utilities.DecodeParameterFromUrl(name);
            var category = Server.Instance.Categories.Categories.SingleOrDefault(c => c.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (category == null)
                return RedirectToAction("Index", "Home");

            ViewData.Model = category;
            ViewData["galleries"] = await Server.Instance.Galleries.GetGalleriesAsync(category, p, 4);
            return View();
        }
    }
}
