using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrator")]
    public class UsersController : Controller
    {
        // GET: /admin/users?q=search&s=name&d=asc&p=2
        public async Task<ActionResult> Index(string q, string s, string d, int p = 1)
        {
            var sortBy = Enum.TryParse<UserSortBy>(s, true, out var parsedSortBy) && Enum.IsDefined(parsedSortBy) ? parsedSortBy : UserSortBy.Created;

            var descending = d.HasValue() ? d == "desc" : DefaultsToDescending(sortBy);

            var users = await Server.Instance.Users.GetUsersAsync(q, sortBy, descending, p, 50);
            ViewData.Model = users;
            ViewData["commentCounts"] = await Server.Instance.Users.GetUserCommentCountsAsync(users.Results.Select(u => u.Id).ToList());
            ViewData["searchString"] = q;
            ViewData["sortBy"] = sortBy;
            ViewData["descending"] = descending;
            return View();
        }

        /// <summary>
        /// Dates and counts read best largest first, names and emails A-Z.
        /// </summary>
        public static bool DefaultsToDescending(UserSortBy sortBy) => sortBy is UserSortBy.Created or UserSortBy.Comments;

        // GET: /admin/users/details/5
        public async Task<ActionResult> Details(string id)
        {
            var user = await Server.Instance.Users.GetUserAsync(id);
            ViewData.Model = user;
            ViewData["galleriesCount"] = await Server.Instance.Users.GetUserGalleryCountAsync(user);
            ViewData["commentsCount"] = await Server.Instance.Users.GetUserCommentCountAsync(user);
            return View();
        }

        // GET: /admin/users/delete/5
        public async Task<ActionResult> Delete(string id)
        {
            var loggedInUserId = Helpers.GetUserId(User);
            var user = await Server.Instance.Users.GetUserAsync(id);
            ViewData.Model = user;
            ViewData["ownAccount"] = loggedInUserId == user.Id;
            ViewData["galleriesCount"] = await Server.Instance.Users.GetUserGalleryCountAsync(user);
            ViewData["commentsCount"] = await Server.Instance.Users.GetUserCommentCountAsync(user);
            return View();
        }

        // POST: /admin/users/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(string id, IFormCollection collection)
        {
            try
            {
                var user = await Server.Instance.Users.GetUserAsync(id);
                if (user.Name == User.Identity.Name)
                    throw new InvalidOperationException("You cannot delete your own account.");

                await Server.Instance.Users.DeleteUserAsync(user);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
                return View();
            }
        }
    }
}
