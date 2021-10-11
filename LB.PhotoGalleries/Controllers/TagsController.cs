using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class TagsController : Controller
    {
        // GET: /t/motogp?p=1
        public async Task<ActionResult> Details(string tag, int p = 1, string s = "", string r = "", string d = "")
        {
            Enum.TryParse(s, true, out QuerySortBy querySortBy);
            Enum.TryParse(r, true, out QueryRange queryRange);
            Enum.TryParse(d, true, out QueryDirection queryDirection);

            ViewData.Model = await Server.Instance.Images.GetImagesForTagAsync(tag, p, 21, querySortBy: querySortBy, queryRange: queryRange, queryDirection: queryDirection);
            ViewData["tag"] = tag;
            return View();
        }
    }
}
