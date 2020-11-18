using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class SearchController : Controller
    {
        public async Task<IActionResult> Index(string q)
        {
            // search categories
            // search gallery names/description
            // search image names/description/tags/credits
            q = q.Trim();
            ViewData["query"] = q;

            // search for categories
            var categories = Server.Instance.Categories.Categories.Where(c => c.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
            ViewData["categories"] = categories;

            // these two queries take 300-400 milliseconds, so run them in parallel:
            var tasks = new List<Task>
            {
                Task.Run(async () =>
                {
                    // search for galleries
                    var galleries = await Server.Instance.Galleries.SearchForGalleriesAsync(q);
                    ViewData["galleries"] = galleries;
                }),
                Task.Run(async () =>
                {
                    // search for images
                    var images = await Server.Instance.Images.SearchForImagesAsync(q, includeInactiveGalleries: true);
                    ViewData["images"] = images;
                })
            };
            await Task.WhenAll(tasks);

            return View();
        }
    }
}
