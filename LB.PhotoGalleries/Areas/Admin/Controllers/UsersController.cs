﻿using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrator")]
    public class UsersController : Controller
    {
        // GET: /admin/users
        public async Task<ActionResult> Index()
        {
            ViewData.Model = await Server.Instance.Users.GetLatestUsersAsync(50);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(string searchString)
        {
            ViewData.Model = await Server.Instance.Users.SearchForUsers(searchString, 50);
            return View();
        }

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
