using System.Diagnostics;
using System.Threading.Tasks;
using LB.PhotoGalleries.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LB.PhotoGalleries.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// Signs the user out. If the user access another page that requires authorisation then they'll be asked to authenticate again.
        /// </summary>
        [Authorize]
        public async Task<IActionResult> SignOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(SignedOut));
        }

        /// <summary>
        /// Displays a message to the user to let them know they've been signed-out.
        /// </summary>
        public IActionResult SignedOut()
        {
            return View();
        }

        /// <summary>
        /// Debug page to display the users claims received from the IDP.
        /// </summary>
        [Authorize]
        public IActionResult Claims()
        {
            return View();
        }
    }
}