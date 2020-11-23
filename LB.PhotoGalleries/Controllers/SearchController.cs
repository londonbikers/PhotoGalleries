using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class SearchController : Controller
    {
        public async Task<IActionResult> Index(string q, int p = 1)
        {
            if (string.IsNullOrEmpty(q))
                return RedirectToAction("Index", "Home");

            // search categories
            // search gallery names/description
            // search image names/description/tags/credits
            q = q.Trim();
            ViewData["query"] = q;
            const int pageSize = 21;

            // don't allow invalid page numbers
            if (p < 1)
                p = 1;

            // search for categories
            var categories = Server.Instance.Categories.Categories.Where(c => c.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
            ViewData["categories"] = categories;

            // these two queries take 300-400 milliseconds, so run them in parallel:
            PagedResultSet<Gallery> galleryPagedResultSet = null;
            PagedResultSet<Image> imagePagedResultSet = null;
            var tasks = new List<Task>
            {
                Task.Run(async () =>
                {
                    // search for galleries
                    galleryPagedResultSet = await Server.Instance.Galleries.SearchForGalleriesAsync(q, p, pageSize);
                }),
                Task.Run(async () =>
                {
                    // search for images
                    imagePagedResultSet = await Server.Instance.Images.SearchForImagesAsync(q, p, pageSize, includeInactiveGalleries:true);
                })
            };
            await Task.WhenAll(tasks);

            // merge the individual paged result sets into a multi-object-type one here
            var searchPagedResultSet = new SearchPagedResultSet
            {
                PageSize = pageSize,
                CategoryResults = categories,
                TotalCategoryResults = categories.Count,
                GalleryResults = galleryPagedResultSet.Results,
                TotalGalleryResults = galleryPagedResultSet.TotalResults,
                ImageResults = imagePagedResultSet.Results,
                TotalImageResults = imagePagedResultSet.TotalResults,
                CurrentPage = p,
                QueryString = $"q={q}"
            };

            ViewData.Model = searchPagedResultSet;
            return View();
        }
    }
}
