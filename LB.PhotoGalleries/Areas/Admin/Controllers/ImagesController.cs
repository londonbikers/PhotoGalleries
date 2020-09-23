﻿using LB.PhotoGalleries.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LB.PhotoGalleries.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrator,Photographer")]
    public class ImagesController : Controller
    {
        #region members
        private readonly IConfiguration _configuration;
        #endregion

        #region constructors
        public ImagesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        #endregion

        // GET: /admin/images/edit/5/5/7
        public async Task<ActionResult> Edit(string categoryId, string galleryId, string imageId)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData.Model = image;
            ViewData["gallery"] = gallery;
            ViewData["tags"] = string.Join(',', image.Tags);
            ViewData["isAuthorisedToEdit"] = Utilities.CanUserEditObject(User, gallery.CreatedByUserId);
            ViewData["mapsKey"] = _configuration["Google:MapsApiKey"];
            return View();
        }

        // POST: /admin/images/edit/5/6/7
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string categoryId, string galleryId, string imageId, IFormCollection collection)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData.Model = image;
            ViewData["gallery"] = gallery;
            ViewData["isAuthorisedToEdit"] = Utilities.CanUserEditObject(User, gallery.CreatedByUserId);
            ViewData["mapsKey"] = _configuration["Google:MapsApiKey"];

            try
            {
                // check that the user is authorised to edit the image, i.e. they're an administrator or the creator of the gallery
                if (!(bool)ViewData["isAuthorisedToEdit"])
                {
                    ViewData["error"] = "Sorry, you are not authorised to edit this image. You did not create the gallery.";
                    return View();
                }

                image.Name = collection["Name"];
                image.Caption = collection["Caption"];
                image.Credit = collection["Credit"];
                image.Tags.Clear();
                image.Tags.AddRange(collection["tagsCsv"].ToString().Split(','));
                ViewData["tags"] = string.Join(',', image.Tags);
                await Server.Instance.Images.UpdateImageAsync(image);
                ViewData["success"] = "Image updated!";
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
            }

            return View();
        }

        // GET: /admin/images/delete/5
        public async Task<ActionResult> Delete(string categoryId, string galleryId, string imageId)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            ViewData.Model = image;
            ViewData["gallery"] = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData["mapsKey"] = _configuration["Google:MapsApiKey"];
            return View();
        }

        // POST: /admin/images/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(string categoryId, string galleryId, string imageId, IFormCollection collection)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData.Model = image;
            ViewData["gallery"] = gallery;
            ViewData["mapsKey"] = _configuration["Google:MapsApiKey"];

            try
            {
                // check that the user is authorised to delete the image, i.e. they're an administrator or the creator of the gallery
                if (!Utilities.CanUserEditObject(User, gallery.CreatedByUserId))
                {
                    ViewData["error"] = "Sorry, you are not authorised to delete this image. You did not create this gallery.";
                    return View();
                }

                await Server.Instance.Images.DeleteImageAsync(image);
                return RedirectToAction("Edit", "Galleries", new { pk = categoryId, id = galleryId });
            }
            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
                return View();
            }
        }
    }
}
