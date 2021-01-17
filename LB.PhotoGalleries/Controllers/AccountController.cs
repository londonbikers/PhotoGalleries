using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class AccountController : Controller
    {
        public async Task<IActionResult> Index()
        {
            ViewData.Model = await Server.Instance.Users.GetUserAsync(Helpers.GetUserId(User));
            return View();
        }
    }
}
