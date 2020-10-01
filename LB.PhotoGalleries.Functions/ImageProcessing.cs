using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Imageflow.Fluent;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Functions
{
    public static class ImageProcessing
    {
        #region public methods
        [FunctionName("ImageProcessingQueueStart")]
        public static Task Run([QueueTrigger("images-to-process", Connection = "Storage:ConnectionString")] string queueMessage, [DurableClient] IDurableOrchestrationClient starter)
        {
            // Orchestration queueMessage comes from the queue message content.
            return starter.StartNewAsync("ImageProcessingOrchestrator", Guid.NewGuid().ToString(), queueMessage);
        }

        [FunctionName("ImageProcessingOrchestrator")]
        public static async Task RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            log = context.CreateReplaySafeLogger(log);

            var ids = context.GetInput<string>().Split(':');
            var imageId = ids[0];
            var galleryId = ids[1];

            log.LogInformation($"ImageProcessing.ImageProcessingOrchestrator() - imageId: {imageId}, galleryId: {galleryId}");

            // retrieve Image object
            // authenticate with the CosmosDB service and create a client we can re-use
            var cosmosClient = new CosmosClient(Environment.GetEnvironmentVariable("CosmosDB:Uri"), Environment.GetEnvironmentVariable("CosmosDB:PrimaryKey"));
            var database = cosmosClient.GetDatabase(Environment.GetEnvironmentVariable("CosmosDB:DatabaseName"));
            var imageContainer = database.GetContainer(Constants.ImagesContainerName);

            // it's possible that we've picked this message up so quick after the Image was created that Cosmos DB replication hasn't had a chance to make
            // sure the new record is fully available
            Image image = null;
            var getImageTries = 0;
            while (image == null && getImageTries < 500)
            {
                var response = imageContainer.ReadItemAsync<Image>(imageId, new PartitionKey(galleryId)).Result;
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    getImageTries += 1;
                    continue;
                }

                image = response.Resource;
            }
            if (image == null)
            {
                // shouldn't happen, the database should become consistent at some point, but gotta cover all the scenarios
                log.LogError("ImageProcessing.ImageProcessingOrchestrator() - Image could not be retrieved from CosmosDB. Quitting.");
                return;
            }

            // download image bytes
            var downloadTimer = new Stopwatch();
            downloadTimer.Start();

            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("Storage:ConnectionString"));
            var originalContainerClient = blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
            var blobClient = originalContainerClient.GetBlobClient(image.Files.OriginalId);
            var blob = blobClient.Download();
            // ReSharper disable once UseAwaitUsing - await not supported at this point in Azure Function
            using var originalImageStream = blob.Value.Content;
            var imageBytes = Utilities.ConvertStreamToBytes(originalImageStream);
            
            downloadTimer.Stop();
            log.LogInformation("ImageProcessing.ImageProcessingOrchestrator() - Image downloaded in: " + downloadTimer.Elapsed);

            // create array of file specs
            var specs = new List<FileSpec> { FileSpec.Spec3840, FileSpec.Spec2560, FileSpec.Spec1920, FileSpec.Spec800, FileSpec.SpecLowRes };

            // resize original image file in parallel
            var parallelTasks = new List<Task<ProcessImageResponse>>();
            foreach (var spec in specs)
                parallelTasks.Add(context.CallActivityAsync<ProcessImageResponse>("ProcessImage", new ProcessImageInput(image, imageBytes, spec)));

            await Task.WhenAll(parallelTasks);

            // write the new storage ids back to the Image object
            foreach (var t in parallelTasks)
            {
                switch (t.Result.FileSpec)
                {
                    case FileSpec.Spec3840:
                        image.Files.Spec3840Id = t.Result.StorageId;
                        break;
                    case FileSpec.Spec2560:
                        image.Files.Spec2560Id = t.Result.StorageId;
                        break;
                    case FileSpec.Spec1920:
                        image.Files.Spec1920Id = t.Result.StorageId;
                        break;
                    case FileSpec.Spec800:
                        image.Files.Spec800Id = t.Result.StorageId;
                        break;
                    case FileSpec.SpecLowRes:
                        image.Files.SpecLowResId = t.Result.StorageId;
                        break;
                }
            }

            // update Image in the db
            var replaceResult = imageContainer.ReplaceItemAsync(image, image.Id, new PartitionKey(image.GalleryId)).Result;
            log.LogInformation($"ImageProcessing.ImageProcessingOrchestrator() - Replace Image response: {replaceResult.StatusCode}. Charge: {replaceResult.RequestCharge}");

            // update the gallery thumbnail if this is the first image being added to the gallery
            var galleryContainer = database.GetContainer(Constants.GalleriesContainerName);
            var getGalleryResponse = await galleryContainer.ReadItemAsync<Gallery>(galleryId, new PartitionKey(image.GalleryCategoryId));
            log.LogInformation($"ImageProcessing.ImageProcessingOrchestrator() - Get gallery request charge: {getGalleryResponse.RequestCharge}");
            var gallery = getGalleryResponse.Resource;

            if (string.IsNullOrEmpty(gallery.ThumbnailStorageId))
            {
                // todo: change this so we write the whole Files property to the gallery so we can choose high-res versions as needed
                gallery.ThumbnailStorageId = image.Files.Spec800Id;
                log.LogInformation($"ImageProcessing.ImageProcessingOrchestrator() - First image, setting gallery thumbnail. galleryId {gallery.Id}, galleryCategoryId {gallery.CategoryId}");

                // update the gallery in the db
                var updateGalleryResponse = await galleryContainer.ReplaceItemAsync(gallery, gallery.Id, new PartitionKey(gallery.CategoryId));
                log.LogInformation("ImageProcessing.ImageProcessingOrchestrator() - Update gallery request charge: " + updateGalleryResponse.RequestCharge);
            }

            // todo: in the future: expire Image cache item when we implement domain caching
        }

        /// <summary>
        /// Produces a new image file, stores it returns the storage id.
        /// </summary>
        /// <param name="input">The input object containing what we need to process the image.</param>
        /// <param name="log">The logging interface.</param>
        /// <returns>The storage id of the resized image, i.e. 465ds4f5d4d54ds465we.webp.</returns>
        [FunctionName("ProcessImage")]
        public static async Task<ProcessImageResponse> ProcessImage([ActivityTrigger] ProcessImageInput input, ILogger log)
        {
            log.LogInformation($"ImageProcessing.ProcessImage() - Image {input.Image.Id} for file spec {input.FileSpec}");

            var imageFileSpec = ImageFileSpecs.GetImageFileSpec(input.FileSpec);

            // we only generate images if the source image is larger than the size we're being asked to resize to, i.e. we only go down in size
            var longestSide = input.Image.Metadata.Width > input.Image.Metadata.Height ? input.Image.Metadata.Width : input.Image.Metadata.Height;
            if (longestSide <= imageFileSpec.PixelLength)
            {
                log.LogWarning($"ImageProcessing.ProcessImage() - image too small for this file spec: {input.FileSpec}: {input.Image.Metadata.Width} x {input.Image.Metadata.Height}");
                return new ProcessImageResponse(input.FileSpec, null);
            }

            // create the new image file
            var storageId = imageFileSpec.GetStorageId(input.Image);
            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("Storage:ConnectionString"));
            await using (var imageFile = await GenerateImageAsync(input.Image, input.ImageBytes, imageFileSpec, log))
            {
                // upload the new image file to storage (delete any old version first)
                var containerClient = blobServiceClient.GetBlobContainerClient(imageFileSpec.ContainerName);

                // ensure this is repeatable, delete any previous blob that may have been generated
                await containerClient.DeleteBlobIfExistsAsync(storageId, DeleteSnapshotsOption.IncludeSnapshots);
                await containerClient.UploadBlobAsync(storageId, imageFile);
            }

            return new ProcessImageResponse(input.FileSpec, storageId);
        }
        #endregion

        #region private methods

        /// <summary>
        /// Generates an resized image of the original in WebP format (quicker, smaller files).
        /// </summary>
        /// <param name="image">The Image the new file should be generated for. Used for logging purposes.</param>
        /// <param name="originalImage">The byte array for the original image.</param>
        /// <param name="imageFileSpec">The name of the image file specification to base the new image.</param>
        /// <param name="log">Enables logging.</param>
        /// <returns>A new image stream for the resized image</returns>
        private static async Task<Stream> GenerateImageAsync(Image image, byte[] originalImage, ImageFileSpec imageFileSpec, ILogger log)
        {
            var timer = new Stopwatch();
            timer.Start();

            using var job = new ImageJob();
            var result = await job.Decode(originalImage)
                //.ConstrainWithin((uint?)imageFileSpec.PixelLength, (uint?)imageFileSpec.PixelLength, new ResampleHints().SetSharpen(41.0f, SharpenWhen.Always).SetResampleFilters(InterpolationFilter.Robidoux, InterpolationFilter.Cubic))
                .ConstrainWithin((uint?)imageFileSpec.PixelLength, (uint?)imageFileSpec.PixelLength, new ResampleHints().SetSharpen(35.0f, SharpenWhen.Downscaling).SetResampleFilters(InterpolationFilter.Robidoux, null))
                .EncodeToBytes(new WebPLossyEncoder(imageFileSpec.Quality))
                .Finish()
                .SetSecurityOptions(new SecurityOptions()
                    .SetMaxDecodeSize(new FrameSizeLimit(12000, 12000, 100))
                    .SetMaxFrameSize(new FrameSizeLimit(12000, 12000, 100))
                    .SetMaxEncodeSize(new FrameSizeLimit(12000, 12000, 30)))
                .InProcessAsync();

            var newImageBytes = result.First.TryGetBytes();
            if (newImageBytes.HasValue)
            {
                var newStream = new MemoryStream(newImageBytes.Value.ToArray());

                timer.Stop();
                log.LogInformation($"ImageProcessing.GenerateImageAsync() - Image {image.Id} and spec {imageFileSpec.FileSpec} done. Image generation time: {timer.Elapsed}");
                return newStream;
            }

            timer.Stop();
            log.LogWarning($"ImageProcessing.GenerateImageAsync() - Couldn't generate new image for {image.Id}! Elapsed time: {timer.Elapsed}");
            return null;
        }
        #endregion
    }
}
