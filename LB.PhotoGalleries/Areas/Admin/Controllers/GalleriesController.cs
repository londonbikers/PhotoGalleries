using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
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
    [Authorize(Roles = "Administrator,Photographer")]
    public class GalleriesController : Controller
    {
        // GET: /admin/galleries
        public async Task<ActionResult> Index()
        {
            ViewData.Model = await Server.Instance.Galleries.GetLatestGalleriesAsync(50);
            return View();
        }

        // GET: /admin/galleries/create
        public ActionResult Create()
        {
            return View();
        }

        // POST: /admin/galleries/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(Gallery gallery)
        {
            try
            {
                gallery.Id = Helpers.CreateNewId();
                gallery.CreatedByUserId = Helpers.GetUserId(User);
                await Server.Instance.Galleries.CreateGalleryAsync(gallery);
                return RedirectToAction(nameof(Edit), new { pk = gallery.CategoryId, id = gallery.Id });
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
                return View();
            }
        }

        // GET: /admin/galleries/edit/5/6
        public async Task<ActionResult> Edit(string pk, string id)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);
            var images = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            var createdByUser = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);

            ViewData.Model = gallery;
            ViewData["images"] = images;
            ViewData["createdByUser"] = createdByUser;
            ViewData["isAuthorisedToEdit"] = Helpers.CanUserEditObject(User, createdByUser);
            return View();
        }

        // POST: /admin/galleries/edit/5/6
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string pk, string id, Gallery gallery)
        {
            var appGallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);
            var createdByUser = await Server.Instance.Users.GetUserAsync(appGallery.CreatedByUserId);
            var images = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            ViewData["isAuthorisedToEdit"] = Helpers.CanUserEditObject(User, createdByUser.Id);
            var inErrorState = false;

            try
            {
                // check that the user is authorised to edit the gallery, i.e. they're an administrator or the creator of the gallery
                if (!Helpers.CanUserEditObject(User, appGallery.CreatedByUserId))
                {
                    ViewData["error"] = "Sorry, you are not authorised to edit this gallery. You did not create it.";
                    inErrorState = true;
                }
                else if (!appGallery.Active && gallery.Active && images.Count == 0)
                {
                    // don't allow publication if there's no images
                    ViewData["error"] = "Please upload some photos before trying to make the gallery active.";
                    inErrorState = true;
                }
                else if (!appGallery.Active && gallery.Active && images.Any(i => string.IsNullOrEmpty(i.Files.Spec800Id)))
                {
                    // don't allow publication if all images are not yet pre-generated
                    ViewData["error"] = "Please wait until all images have been processed before making the gallery active.";
                    inErrorState = true;
                }

                if (!inErrorState)
                {
                    // map attributes from form gallery to one retrieved from the app server
                    appGallery.Name = gallery.Name;
                    appGallery.Description = gallery.Description;
                    appGallery.Active = gallery.Active;

                    await Server.Instance.Galleries.UpdateGalleryAsync(appGallery);
                    ViewData["success"] = "Gallery updated!";
                }
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
            }

            ViewData.Model = appGallery;
            ViewData["username"] = createdByUser.Name;
            ViewData["images"] = images;

            return View();
        }

        // GET: /admin/galleries/changecategory/5/6
        public async Task<ActionResult> ChangeCategory(string pk, string id)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);
            var createdByUser = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
            ViewData["isAuthorisedToEdit"] = Helpers.CanUserEditObject(User, createdByUser.Id);
            ViewData.Model = gallery;
            return View();
        }

        // POST: /admin/galleries/changecategory/5/6
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeCategory(string pk, string id, IFormCollection collection)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);
            var createdByUser = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
            var isAuthorisedToEdit = Helpers.CanUserEditObject(User, createdByUser.Id);
            var newCategoryId = collection["CategoryId"];
            ViewData["isAuthorisedToEdit"] = isAuthorisedToEdit;
            ViewData.Model = gallery;

            if (!isAuthorisedToEdit)
                return View();

            // change gallery category
            await Server.Instance.Galleries.ChangeGalleryCategoryAsync(pk, id, newCategoryId);
            return RedirectToAction(nameof(Edit), new { pk = newCategoryId, id = gallery.Id });
        }

        // GET: /admin/galleries/delete/5/6
        public async Task<ActionResult> Delete(string pk, string id)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);
            if (gallery.CreatedByUserId.HasValue())
                ViewData["createdByUser"] = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);

            ViewData.Model = gallery;
            ViewData["images"] = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            ViewData["isAuthorisedToEdit"] = Helpers.CanUserEditObject(User, gallery.CreatedByUserId);
            ViewData["category"] = Server.Instance.Categories.Categories.Single(q => q.Id == gallery.CategoryId);
            return View();
        }

        // POST: /admin/galleries/delete/5/6
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(string pk, string id, IFormCollection collection)
        {
            // set the view up in case we have to error out to it
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);

            if (gallery.CreatedByUserId.HasValue())
            {
                ViewData["createdByUser"] = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
                ViewData["isAuthorisedToEdit"] = User.IsInRole("Administrator") || gallery.CreatedByUserId == Helpers.GetUserId(User);
            }
            else
            {
                ViewData["isAuthorisedToEdit"] = User.IsInRole("Administrator");
            }

            ViewData.Model = gallery;
            ViewData["images"] = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            ViewData["category"] = Server.Instance.Categories.Categories.Single(q => q.Id == gallery.CategoryId);

            try
            {
                // check the user is authorised to delete the gallery
                if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
                {
                    ViewData["error"] = "Sorry, you are not authorised to edit this gallery. You did not create it.";
                    return View();
                }

                await Server.Instance.Galleries.DeleteGalleryAsync(pk, id);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
                return View();
            }
        }

        #region admin methods
        // GET: /admin/galleries/missingthumbnails
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult> MissingThumbnails()
        {
            ViewData["count"] = await Server.Instance.Galleries.GetMissingThumbnailGalleriesCountAsync();
            return View();
        }

        // GET: /admin/galleries/missingthumbnails
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<ActionResult> MissingThumbnails(IFormCollection collection)
        {
            await Server.Instance.Galleries.AssignMissingThumbnailsAsync();
            ViewData["success"] = "Thumbnails assigned!";
            ViewData["count"] = await Server.Instance.Galleries.GetMissingThumbnailGalleriesCountAsync();
            return View();
        }
        #endregion
    }
}
