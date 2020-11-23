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
            const int maxResults = 500;

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
                    galleryPagedResultSet = await Server.Instance.Galleries.SearchForGalleriesAsync(q, p, pageSize, maxResults);
                }),
                Task.Run(async () =>
                {
                    // search for images
                    imagePagedResultSet = await Server.Instance.Images.SearchForImagesAsync(q, p, pageSize, maxResults, includeInactiveGalleries:true);
                })
            };
            await Task.WhenAll(tasks);

            // merge the individual paged result sets into a multi-object-type one here
            var searchPagedResultSet = new SearchPagedResultSet
            {
                PageSize = pageSize,
                CurrentPage = p,
                QueryString = $"q={q}",
                MaximumResults = maxResults
            };

            if (p == 1)
            {
                searchPagedResultSet.CategoryResults = categories;
                searchPagedResultSet.TotalCategoryResults = categories.Count;
            }

            if (galleryPagedResultSet != null)
            {
                searchPagedResultSet.GalleryResults = galleryPagedResultSet.Results;
                searchPagedResultSet.TotalGalleryResults = galleryPagedResultSet.TotalResults;
            }

            if (imagePagedResultSet != null)
            {
                searchPagedResultSet.ImageResults = imagePagedResultSet.Results;
                searchPagedResultSet.TotalImageResults = imagePagedResultSet.TotalResults;
            }

            ViewData.Model = searchPagedResultSet;
            return View();
        }
    }
}
