using Azure.Storage.Blobs;
using Imageflow.Fluent;
using Imageflow.Server;
using Imageflow.Server.Storage.AzureBlob;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Drawing;
using System.IO;

namespace LB.PhotoGalleries
{
    public static class ImageResizing
    {
        public static void InitialiseImageFlowDiskCache(IConfiguration configuration)
        {
            var path = configuration["ImageFlow:DiskCachePath"];
            Log.Debug("InitialiseImageFlowDiskCache: path: " + path);
            if (Directory.Exists(path))
                return;

            Log.Debug("InitialiseImageFlowDiskCache: creating new path");
            Directory.CreateDirectory(path);
            Log.Debug("InitialiseImageFlowDiskCache: created new path");
        }

        public static void AddImageFlowBlobService(IConfiguration configuration, IServiceCollection services, FileSpec fileSpec, string path)
        {
            var imageFlowSpec = ImageFileSpecs.GetImageFileSpec(fileSpec);
            services.AddImageflowAzureBlobService(new AzureBlobServiceOptions(configuration["Storage:ConnectionString"], new BlobClientOptions())
                .MapPrefix(path, imageFlowSpec.ContainerName));
        }

        public static void AddWatermark(WatermarkingEventArgs args)
        {
            // PROBLEM: we don't know when an image is portrait when default incoming dims are equal!
            // need original dims, don't want to trust client

            var modeSpecified = args.Query.ContainsKey("mode");
            var size = new Size();
            if (args.Query.ContainsKey("w") && int.TryParse(args.Query["w"], out var wParam))
                size.Width = wParam;
            else if (args.Query.ContainsKey("width") && int.TryParse(args.Query["width"], out var widthParam))
                size.Width = widthParam;
            if (args.Query.ContainsKey("h") && int.TryParse(args.Query["h"], out var hParam))
                size.Height = hParam;
            else if (args.Query.ContainsKey("height") && int.TryParse(args.Query["height"], out var heightParam))
                size.Height = heightParam;

            var imageSizeRequiresWatermark = size.Width < 1 || size.Height < 1 || (size.Width > 1000 || size.Height > 1000);
            var referer = args.Context.Request.GetTypedHeaders().Referer;
            var isLocalReferer = referer != null && referer.Host.Equals(args.Context.Request.Host.Host, StringComparison.CurrentCultureIgnoreCase);
            if (modeSpecified && isLocalReferer && !imageSizeRequiresWatermark)
                return;

            // the watermark needs to be a bit bigger when displayed on portrait format images
            var watermarkSizeAsPercent = 13;
            if ((args.Query.ContainsKey("o") && args.Query["o"] == "p") || size.Height > size.Width)
                watermarkSizeAsPercent = 25;

            args.AppliedWatermarks.Add(
                new NamedWatermark("lb-corner-logo", "/local-images/lb-white-stroked-10.png",
                new WatermarkOptions()
                    .SetFitBoxLayout(new WatermarkFitBox(WatermarkAlign.Image, 1, 10, watermarkSizeAsPercent, 99), WatermarkConstraintMode.Within, new ConstraintGravity(0, 100))));
        }

        /// <summary>
        /// Original images should only be requested by legacy clients and should be size-constrained so the endpoint can't be used to download the entire original image
        /// </summary>
        public static void EnsureOriginalImageDimensionsAreLimited(UrlEventArgs args)
        {
            const int maxSize = 3840;
            var width = maxSize;
            var height = maxSize;

            if (args.Query.ContainsKey("w"))
                width = int.Parse(args.Query["w"]);
            else if (args.Query.ContainsKey("width"))
                width = int.Parse(args.Query["w"]);

            if (args.Query.ContainsKey("h"))
                height = int.Parse(args.Query["h"]);
            else if (args.Query.ContainsKey("height"))
                height = int.Parse(args.Query["h"]);

            if (width > maxSize)
                width = maxSize;

            if (height > maxSize)
                height = maxSize;

            if (!args.Query.ContainsKey("mode"))
                args.Query["mode"] = "max";

            args.Query["w"] = width.ToString();
            args.Query["h"] = height.ToString();
        }

        public static void EnsureDi3840DimensionsAreSpecified(UrlEventArgs args)
        {
            var containsWidthParam = args.Query.ContainsKey("w") || args.Query.ContainsKey("width");
            var containsHeightParam = args.Query.ContainsKey("h") || args.Query.ContainsKey("height");
            if (containsWidthParam || containsHeightParam)
                return;

            args.Query["w"] = 3840.ToString();
            args.Query["h"] = 3840.ToString();
        }

        public static void EnsureDi2560DimensionsAreSpecified(UrlEventArgs args)
        {
            var containsWidthParam = args.Query.ContainsKey("w") || args.Query.ContainsKey("width");
            var containsHeightParam = args.Query.ContainsKey("h") || args.Query.ContainsKey("height");
            if (containsWidthParam || containsHeightParam)
                return;

            args.Query["w"] = 2560.ToString();
            args.Query["h"] = 2560.ToString();
        }

        public static void EnsureDi1920DimensionsAreSpecified(UrlEventArgs args)
        {
            var containsWidthParam = args.Query.ContainsKey("w") || args.Query.ContainsKey("width");
            var containsHeightParam = args.Query.ContainsKey("h") || args.Query.ContainsKey("height");
            if (containsWidthParam || containsHeightParam)
                return;

            args.Query["w"] = 1920.ToString();
            args.Query["h"] = 1920.ToString();
        }

        public static void OpenGraphImageHandler(UrlEventArgs args)
        {
            args.Query["w"] = 2048.ToString();
            args.Query["h"] = 2048.ToString();
            args.Query["mode"] = "max";
        }
    }
}
