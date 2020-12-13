using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class SearchController : Controller
    {
        public async Task<IActionResult> Index(string q, int p = 1, SearchResultsType t = SearchResultsType.All)
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

            List<Category> categories = null;
            if (t == SearchResultsType.All || t == SearchResultsType.Categories)
            {
                // search for categories
                categories = Server.Instance.Categories.Categories.Where(c => c.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
                ViewData["categories"] = categories;
            }

            PagedResultSet<Gallery> galleryPagedResultSet = null;
            PagedResultSet<Image> imagePagedResultSet = null;
            if (t != SearchResultsType.Categories)
            {
                if (t == SearchResultsType.Galleries)
                {
                    // search for just galleries
                    galleryPagedResultSet = await Server.Instance.Galleries.SearchForGalleriesAsync(q, p, pageSize, maxResults);
                } 
                else if (t== SearchResultsType.Images)
                {
                    // search for just images
                    imagePagedResultSet = await Server.Instance.Images.SearchForImagesAsync(q, p, pageSize, maxResults, includeInactiveGalleries: true);
                }
                else
                {
                    // these two queries take 300-400 milliseconds, so run them in parallel:
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

                    var stopwatch = Stopwatch.StartNew();
                    await Task.WhenAll(tasks);
                    stopwatch.Stop();
                    Debug.WriteLine($"SearchController.Index: Search took {stopwatch.ElapsedMilliseconds} ms");
                }
            }

            // merge the individual paged result sets into a multi-type one here
            var searchPagedResultSet = new SearchPagedResultSet
            {
                PageSize = pageSize,
                CurrentPage = p,
                QueryString = $"q={q}",
                MaximumResults = maxResults,
                SearchResultsType = t
            };

            if (p == 1 && categories != null)
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

            ViewData["galleriesJson"] = searchPagedResultSet.GalleryResults != null ? JsonConvert.SerializeObject(searchPagedResultSet.GalleryResults.Select(g => new
            {
                g.CategoryId,
                g.Id,
                g.Name,
                g.ThumbnailFiles,
                g.ImageCount,
                CategoryName = Server.Instance.Categories.GetCategory(g.CategoryId).Name
            })) : null;

            ViewData["imagesJson"] = searchPagedResultSet.ImageResults != null ? JsonConvert.SerializeObject(searchPagedResultSet.ImageResults.Select(i => new
            {
                i.Id,
                i.GalleryId,
                i.Name,
                i.Files
            })) : null;

            return View();
        }
    }
}
