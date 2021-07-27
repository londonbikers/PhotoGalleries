using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers
{
    public class GalleriesController : Controller
    {
        #region members
        private readonly IConfiguration _configuration;
        #endregion

        #region constructors
        public GalleriesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        #endregion

        // GET: /g/<category>/<galleryId>/<name>
        public async Task<ActionResult> Details(string categoryName, string galleryId, string name)
        {
            ViewData["Configuration"] = _configuration;
            var decodedCategoryName = Helpers.DecodeParameterFromUrl(categoryName);
            var category = Server.Instance.Categories.Categories.SingleOrDefault(c => c.Name.Equals(decodedCategoryName, StringComparison.CurrentCultureIgnoreCase));
            if (category == null)
                return RedirectToAction("Index");

            var gallery = await Server.Instance.Galleries.GetGalleryAsync(category.Id, galleryId);
            if (gallery == null)
                return RedirectToAction("Index");

            var images = Utilities.OrderImages(await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id)).ToList();
            ViewData["images"] = images;

            if (gallery.CreatedByUserId.HasValue())
            {
                // migrated galleries won't have a user id
                ViewData["user"] = await Server.Instance.Users.GetUserAsync(gallery.CreatedByUserId);
            }

            ViewData.Model = gallery;

            long pixels = 0;
            foreach (var image in (List<Image>) ViewData["images"])
                if (image.Metadata.Width.HasValue && image.Metadata.Height.HasValue)
                    pixels += image.Metadata.Width.Value * image.Metadata.Height.Value;

            var megapixels = Convert.ToInt32(pixels / 1000000);
            ViewData["megapixels"] = megapixels;

            // build the open-graph model to enable great presentation when pages are indexed/shared
            var openGraphModel = new OpenGraphModel {Title = gallery.Name, Url = Request.GetRawUrl().AbsoluteUri};

            if (images.Count > 0)
            {
                var image = images[0];
                var openGraphImage = new OpenGraphModel.OpenGraphImageModel { Url = $"{_configuration["BaseUrl"]}diog/{image.Files.OriginalId}" };

                if (image.Metadata.Width.HasValue && image.Metadata.Height.HasValue)
                {
                    int width;
                    int height;

                    if (image.Metadata.Width > image.Metadata.Height)
                    {
                        width = 1080;
                        var dHeight = (decimal) image.Metadata.Height / ((decimal) image.Metadata.Width / (decimal) 1080);
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
            }

            if (!string.IsNullOrEmpty(gallery.Description))
                openGraphModel.Description = Helpers.GetFirstParagraph(gallery.Description);
            ViewData["openGraphModel"] = openGraphModel;

            return View();
        }
    }
}
