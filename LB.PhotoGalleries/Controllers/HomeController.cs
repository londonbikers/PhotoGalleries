using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class HomeController : Controller
    {
        public async Task<IActionResult> Index()
        {
            ViewData["galleries"] = await Server.Instance.Galleries.GetLatestActiveGalleriesAsync(12);
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // access the unhandled exception and log it
            var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionHandlerPathFeature != null && exceptionHandlerPathFeature.Error != null)
                Log.Error(exceptionHandlerPathFeature.Error, "Unhandled exception!");

            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// Signs the user in by redirecting them to our IDP.
        /// </summary>
        [Authorize]
        public IActionResult SignIn(string returnUrl = null)
        {
            // by this point the user will be logged in and we can redirect them back to the homepage for now.
            // in the future we might want to redirect the back to a particular page, so we'll need to add a local redirect URL parameter we can pick up on

            if (returnUrl.HasValue())
            {
                // if the user is on the sign-out page, redirect them home instead as otherwise
                // they sign-in and are told they're signed out, which isn't very helpful.

                if (returnUrl.Equals("/home/signedout", StringComparison.CurrentCultureIgnoreCase))
                    return RedirectToAction(nameof(Index));

                // otherwise take them back to where they wanted to be, as long as it was on our site
                return LocalRedirect(returnUrl);
            }

            // failing not knowing where to send them, send them to the homepage
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Signs the user out. If the user access another page that requires authorisation then they'll be asked to authenticate again.
        /// </summary>
        [Authorize]
        public async Task SignOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync("oidc");
        }

        /// <summary>
        /// Debug page to display the users claims received from the IDP.
        /// </summary>
        [Authorize]
        public IActionResult Claims()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        /// <summary>
        /// Used to test error handling.
        /// </summary>
        /// <returns></returns>
        public IActionResult ErrorTest()
        {
            throw new Exception("Something bad happened, maybe.");
        }
    }
}
