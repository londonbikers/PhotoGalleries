using System;
using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
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

        // GET: /gi/{galleryId}/{imageId}/{name}
        public async Task<ActionResult> Details(string galleryId, string imageId, string name)
        {
            var image = await Server.Instance.Images.GetImageAsync(galleryId, imageId);
            if (image == null)
                return RedirectToActionPermanent("Index", "Home");

            ViewData.Model = image;
            ViewData["gallery"] = await Server.Instance.Galleries.GetGalleryAsync(image.GalleryCategoryId, galleryId);
            ViewData["category"] = Server.Instance.Categories.Categories.Single(c => c.Id == image.GalleryCategoryId);
            ViewData["mapsKey"] = _configuration["Google:MapsApiKey"];

            if (User.Identity.IsAuthenticated)
                ViewData["user"] = await Server.Instance.Users.GetUserAsync(Helpers.GetUserId(User));

            // work out the previous image in the gallery for navigation purposes
            ViewData["previousImage"] = await Server.Instance.Images.GetPreviousImageInGalleryAsync(image);
            ViewData["nextImage"] = await Server.Instance.Images.GetNextImageInGalleryAsync(image);

            // record the image view if the user hasn't viewed the image before in their current session
            var viewedImages = HttpContext.Session.Get<List<string>>("viewedImages") ?? new List<string>();
            if (!viewedImages.Contains(image.Id))
            {
                await Server.Instance.Images.IncreaseImageViewsAsync(galleryId, imageId);
                viewedImages.Add(image.Id);
                HttpContext.Session.Set("viewedImages", viewedImages);
            }

            // build the open-graph model to enable great presentation when pages are indexed/shared
            var openGraphModel = new OpenGraphModel {Title = image.Name, Url = Request.GetRawUrl().AbsoluteUri};

            if (!string.IsNullOrEmpty(image.Caption))
                openGraphModel.Description = Helpers.GetFirstParagraph(image.Caption);

            var openGraphImage = new OpenGraphModel.OpenGraphImageModel { Url = $"{_configuration["BaseUrl"]}diog/{image.Files.OriginalId}" };

            if (image.Metadata.Width.HasValue && image.Metadata.Height.HasValue)
            {
                int width;
                int height;

                if (image.Metadata.Width > image.Metadata.Height)
                {
                    width = 1080;
                    var dHeight = (decimal)image.Metadata.Height / ((decimal)image.Metadata.Width / (decimal)1080);
                    height = (int)Math.Round(dHeight);
                }
                else
                {
                    height = 1080;
                    var dWidth = (decimal)image.Metadata.Width / ((decimal)image.Metadata.Height / (decimal)1080);
                    width = (int)Math.Round(dWidth);
                }

                openGraphImage.Width = width;
                openGraphImage.Height = height;
            }

            openGraphModel.Images.Add(openGraphImage);


            ViewData["openGraphModel"] = openGraphModel;

            return View();
        }
    }
}
