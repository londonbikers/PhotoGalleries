using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
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

        [Route("/account/email-preferences")]
        [HttpGet]
        public async Task<IActionResult> EmailPreferences()
        {
            var user = await Server.Instance.Users.GetUserAsync(Helpers.GetUserId(User));
            ViewData.Model = new EmailPreferencesModel
            {
                ReceiveCommentNotifications = user.CommunicationPreferences.ReceiveCommentNotifications
            };
            return View();
        }

        [Route("/account/email-preferences")]
        [HttpPost]
        public async Task<IActionResult> EmailPreferences(EmailPreferencesModel emailPreferencesModel)
        {
            var user = await Server.Instance.Users.GetUserAsync(Helpers.GetUserId(User));
            user.CommunicationPreferences.ReceiveCommentNotifications = emailPreferencesModel.ReceiveCommentNotifications;
            await Server.Instance.Users.CreateOrUpdateUserAsync(user);
            emailPreferencesModel.EmailPreferencesUpdated = true;
            
            Log.Information($"Web:AccountController.EmailPreferences(): User updated their email preferences: {user.Name}");
            return View(emailPreferencesModel);
        }

        [AllowAnonymous]
        public ActionResult AccessDenied()
        {
            Log.Information("AccessDenied()");
            return View();
        }
    }
}
