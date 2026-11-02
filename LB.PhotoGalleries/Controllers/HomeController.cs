using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers;

public class HomeController : Controller
{
    public async Task<IActionResult> Index()
    {
        var racingCategory = Server.Instance.Categories.Categories.SingleOrDefault(c => c.Name.Equals("racing", StringComparison.CurrentCultureIgnoreCase));
        if (racingCategory != null)
            ViewData["FeaturedGalleries"] = await Server.Instance.Galleries.GetGalleriesAsync(racingCategory, 1, 3, 3);
        else
            ViewData["FeaturedGalleries"] = new PagedResultSet<Gallery>();
            
        ViewData["LatestGalleries"] = await Server.Instance.Galleries.GetLatestActiveGalleriesAsync(12);
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// Error page for unhandled exceptions. Anonymous so it still renders when the site is closed to the public,
    /// otherwise the global authorize filter starts another sign-in, which fails the same way as the original error.
    /// </summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // access the unhandled exception and log it
        var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var error = exceptionHandlerPathFeature?.Error;

        // the idp refused to start a sign-in, e.g. it rejected our client secret (invalid_client)
        if (error is OpenIdConnectProtocolException)
        {
            Log.Error(error, "Sign-in failed, the IdP returned an error: {Message}", error.Message);
            return View(nameof(SignInFailed), new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        if (error != null)
            Log.Error(error, "Unhandled exception!");

        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    /// <summary>
    /// Shown when signing in fails at the IdP. The OIDC OnRemoteFailure event sends users here, and Error() uses its view.
    /// </summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult SignInFailed()
    {
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

        if (!returnUrl.HasValue()) return RedirectToAction(nameof(Index));
        // if the user is on the sign-out page, redirect them home instead as otherwise
        // they sign-in and are told they're signed out, which isn't very helpful.

        if (returnUrl != null && returnUrl.Equals("/home/signedout", StringComparison.CurrentCultureIgnoreCase))
            return RedirectToAction(nameof(Index));

        // otherwise take them back to where they wanted to be, as long as it was on our site
        // failing not knowing where to send them, send them to the homepage
        return LocalRedirect(returnUrl ?? "/");
    }

    /// <summary>
    /// Signs the user out. If the user access another page that requires authorisation then they'll be asked to authenticate again.
    /// </summary>
    [Authorize]
    public new async Task SignOut()
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