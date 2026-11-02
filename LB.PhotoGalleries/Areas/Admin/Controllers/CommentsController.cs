using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrator")]
    public class CommentsController : Controller
    {
        // GET: /admin/comments?q=search&u=userid&p=2&n=100
        public async Task<ActionResult> Index(string q, string u, int p = 1, int? n = null)
        {
            var pageSize = Helpers.GetAdminPageSize(n);
            var comments = await Server.Instance.Comments.GetCommentsAsync(q, u, p, pageSize);
            ViewData.Model = comments;

            var userIds = comments.Results.Select(c => c.CreatedByUserId).Append(u).Where(id => id != null).Distinct().ToList();
            ViewData["users"] = (await Server.Instance.Users.GetUsersByIdsAsync(userIds)).ToDictionary(user => user.Id);
            ViewData["searchString"] = q;
            ViewData["userId"] = u;
            ViewData["pageSize"] = pageSize;
            return View();
        }

        // POST: /admin/comments/delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(string categoryId, string galleryId, string imageId, long createdTicks, string createdByUserId, string returnUrl)
        {
            // if it's not found it's most likely already been deleted, either way, it's gone
            await Server.Instance.Comments.DeleteCommentAsync(categoryId, galleryId, imageId, createdTicks, createdByUserId);
            TempData["message"] = "Comment deleted.";

            if (Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }
    }
}
