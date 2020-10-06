using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

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

        // GET: /admin/images/edit/5/5/7
        public async Task<ActionResult> Edit(string categoryId, string galleryId, string imageId)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            var gallery = await Server.Instance.Galleries.GetGalleryAsync(categoryId, galleryId);
            ViewData.Model = image;
            ViewData["gallery"] = gallery;
            ViewData["tags"] = string.Join(',', image.Tags);
            ViewData["isAuthorisedToEdit"] = Helpers.CanUserEditObject(User, gallery.CreatedByUserId);
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
            ViewData["isAuthorisedToEdit"] = Helpers.CanUserEditObject(User, gallery.CreatedByUserId);
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
                if (!Helpers.CanUserEditObject(User, gallery.CreatedByUserId))
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
