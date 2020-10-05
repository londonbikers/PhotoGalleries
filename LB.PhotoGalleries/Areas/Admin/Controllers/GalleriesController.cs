﻿using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Exceptions;
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
            var createdByUser = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
            ViewData.Model = gallery;
            ViewData["images"] = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            ViewData["username"] = createdByUser.Name;
            ViewData["isAuthorisedToEdit"] = Helpers.CanUserEditObject(User, createdByUser.Id);
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
        
        [HttpPost]
        [RequestSizeLimit(104857600)]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
        public async Task<IActionResult> Upload(string categoryId, string galleryId, IFormFile file)
        {
            // RequestSizeLimit: 104857600 = 100MB
            // store the file in cloud storage and post-process
            // follow secure uploads advice from: https://docs.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-3.1

            if (file.Length == 0)
                return NoContent();

            var stream = file.OpenReadStream();

            try
            {
                await Server.Instance.Images.CreateImageAsync(categoryId, galleryId, stream, file.FileName);
            }
            catch (ImageTooSmallException e)
            {
                return BadRequest(e.Message);
            }
            finally
            {
                if (stream != null)
                    await stream.DisposeAsync();
            }

            return Ok();
        }

        // GET: /admin/galleries/delete/5/6
        public async Task<ActionResult> Delete(string pk, string id)
        {
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);
            var createdByUser = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
            ViewData.Model = gallery;
            ViewData["images"] = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            ViewData["username"] = createdByUser.Name;
            ViewData["isAuthorisedToEdit"] = Helpers.CanUserEditObject(User, gallery.CreatedByUserId);
            ViewData["category"] = Server.Instance.Categories.Categories.Single(q => q.Id == gallery.CategoryId);
            ViewData["createdByUser"] = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
            return View();
        }

        // POST: /admin/galleries/delete/5/6
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(string pk, string id, IFormCollection collection)
        {
            // set the view up in case we have to error out to it
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(pk, id);
            var createdByUser = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
            ViewData.Model = gallery;
            ViewData["images"] = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            ViewData["username"] = createdByUser.Name;
            ViewData["isAuthorisedToEdit"] = User.IsInRole("Administrator") || gallery.CreatedByUserId == Helpers.GetUserId(User);
            ViewData["category"] = Server.Instance.Categories.Categories.Single(q => q.Id == gallery.CategoryId);
            ViewData["createdByUser"] = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);

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
    }
}
