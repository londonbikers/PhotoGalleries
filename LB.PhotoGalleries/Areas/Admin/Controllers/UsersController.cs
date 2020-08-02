using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        // GET: /admin/users/details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: /admin/users/delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: /admin/users/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
