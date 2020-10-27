using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class TagsController : Controller
    {
        // GET: /t/motogp?p=1
        public async Task<ActionResult> Details(string tag, int p = 1)
        {
            ViewData.Model = await Server.Instance.Images.GetImagesAsync(tag, p, 21); ;
            ViewData["tag"] = tag;
            return View();
        }
    }
}
