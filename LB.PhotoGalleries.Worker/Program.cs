using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Imageflow.Fluent;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Worker
{
    /// <summary>
    /// Background worker process for LB Photo Galleries.
    /// Processes an Azure Storage message queue to resize down new versions of uploaded image files.
    /// </summary>
    internal class Program
    {
        #region members
        private static IConfiguration _configuration;
        private static CosmosClient _cosmosClient;
        private static BlobServiceClient _blobServiceClient;
        private static Database _database;
        private static Container _imagesContainer;
        private static ILogger _log;
        #endregion

        private static async Task Main(string[] args)
        {
            Console.WriteLine("Starting worker...");

            // setup configuration
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            // setup logging
            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs\\lb.photogalleries.worker.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                // setup clients/references
                _cosmosClient = new CosmosClient(_configuration["CosmosDB:Uri"], _configuration["CosmosDB:PrimaryKey"]);
                _blobServiceClient = new BlobServiceClient(_configuration["Storage:ConnectionString"]);
                _database = _cosmosClient.GetDatabase(_configuration["CosmosDB:DatabaseName"]);
                _imagesContainer = _database.GetContainer(Constants.ImagesContainerName);

                // set the message queue listener
                var queueName = _configuration["Storage:ImageProcessingQueueName"];
                var queueClient = new QueueClient(_configuration["Storage:ConnectionString"], queueName);
                int.TryParse(_configuration["Storage:MessageBatchSize"], out var messageBatchSize);
                int.TryParse(_configuration["Storage:MessageBatchVisibilityTimeoutMins"], out var messageBatchVisibilityMins);
                if (!await queueClient.ExistsAsync())
                {
                    _log.Fatal($"LB.PhotoGalleries.Worker.Program.Main() - {queueName} queue does not exist. Cannot continue.");
                    return;
                }

                // keep processing the queue until the program is shutdown
                while (true)
                {
                    // get a batch of messages from the queue to process
                    // getting a batch is more efficient as it minimises the number of HTTP calls we have to make
                    var messages = await queueClient.ReceiveMessagesAsync(messageBatchSize, TimeSpan.FromMinutes(messageBatchVisibilityMins));
                    _log.Information($"LB.PhotoGalleries.Worker.Program.Main() - Received {messages.Value.Length} messages from the {queueName} queue ");

                    // todo: make this parallel
                    foreach (var message in messages.Value)
                    {
                        await ProcessImageProcessingMessageAsync(message.MessageText);

                        // as the message was processed successfully, we can delete the message from the queue
                        await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                    }

                    // if we we received messages this iteration then there's a good chance there's more to process so don't pause between polls
                    if (messages.Value.Length == 0)
                    {
                        // todo: implement better back-off functionality
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
            }
            catch (Exception exception)
            {
                _log.Fatal(exception, "LB.PhotoGalleries.Worker.Program.Main() - Unhandled exception!");
            }
        }

        #region private methods
        private static async Task ProcessImageProcessingMessageAsync(string message)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var ids = Utilities.Base64Decode(message).Split(':');
            var imageId = ids[0];
            var galleryId = ids[1];

            _log.Information($"LB.PhotoGalleries.Worker.Program.GetImage() - imageId: {imageId}, galleryId: {galleryId}");

            // retrieve Image object and bytes
            var image = await GetImageAsync(new DatabaseId(imageId, galleryId));
            var imageBytes = await GetImageBytesAsync(image);

            // create array of file specs to iterate over
            var specs = new List<FileSpec> { FileSpec.Spec3840, FileSpec.Spec2560, FileSpec.Spec1920, FileSpec.Spec800, FileSpec.SpecLowRes };

            // resize original image file to smaller versions in parallel
            var parallelTasks = specs.Select(spec => ProcessImageAsync(image, imageBytes, spec)).ToList();
            await Task.WhenAll(parallelTasks);

            await UpdateModelsAsync(image);

            stopwatch.Stop();
            _log.Information($"LB.PhotoGalleries.Worker.Program.GetImage() - Processed {image.Id} in {stopwatch.ElapsedMilliseconds}ms");
        }

        private static async Task<Image> GetImageAsync(DatabaseId databaseId)
        {
            // it's possible that we've picked this message up so quick after the Image was created that Cosmos DB replication hasn't had a chance to make
            // sure the new record is fully available
            var getImageTries = 0;
            while (getImageTries < 500)
            {
                var response = await _imagesContainer.ReadItemAsync<Image>(databaseId.Id, new PartitionKey(databaseId.PartitionKey));
                if (response.StatusCode == HttpStatusCode.OK)
                    return response.Resource;

                getImageTries += 1;
            }

            // shouldn't happen, the database should become consistent at some point, but gotta cover all the scenarios
            _log.Error("ImageProcessing.GetImage() - Image could not be retrieved from CosmosDB. Returning null.");
            return null;
        }

        private static async Task<byte[]> GetImageBytesAsync(Image image)
        {
            // download image bytes
            var downloadTimer = new Stopwatch();
            downloadTimer.Start();

            var originalContainerClient = _blobServiceClient.GetBlobContainerClient(Constants.StorageOriginalContainerName);
            var blobClient = originalContainerClient.GetBlobClient(image.Files.OriginalId);
            var blob = await blobClient.DownloadAsync();

            await using var originalImageStream = blob.Value.Content;
            var imageBytes = Utilities.ConvertStreamToBytes(originalImageStream);

            downloadTimer.Stop();
            _log.Information($"LB.PhotoGalleries.Worker.Program.GetImageBytesAsync() - Image downloaded in: {downloadTimer.ElapsedMilliseconds}ms");

            return imageBytes;
        }

        private static async Task ProcessImageAsync(Image image, byte[] imageBytes, FileSpec fileSpec)
        {
            _log.Information($"LB.PhotoGalleries.Worker.Program.ProcessImageAsync() - Image {image.Id} for file spec {fileSpec}");

            var imageFileSpec = ImageFileSpecs.GetImageFileSpec(fileSpec);

            // we only generate images if the source image is larger than the size we're being asked to resize to, i.e. we only go down in size
            var longestSide = image.Metadata.Width > image.Metadata.Height ? image.Metadata.Width : image.Metadata.Height;
            if (longestSide <= imageFileSpec.PixelLength)
            {
                _log.Warning($"LB.PhotoGalleries.Worker.Program.ProcessImageAsync() - Image too small for this file spec: {fileSpec}: {image.Metadata.Width} x {image.Metadata.Height}");
                return;
            }

            // create the new image file
            var storageId = imageFileSpec.GetStorageId(image);
            await using (var imageFile = await GenerateImageAsync(image, imageBytes, imageFileSpec))
            {
                // upload the new image file to storage (delete any old version first)
                var containerClient = _blobServiceClient.GetBlobContainerClient(imageFileSpec.ContainerName);

                // ensure this is repeatable, delete any previous blob that may have been generated
                var deleteStopwatch = new Stopwatch();
                deleteStopwatch.Start();
                await containerClient.DeleteBlobIfExistsAsync(storageId, DeleteSnapshotsOption.IncludeSnapshots);
                deleteStopwatch.Stop();

                var uploadStopwatch = new Stopwatch();
                uploadStopwatch.Start();
                await containerClient.UploadBlobAsync(storageId, imageFile);
                uploadStopwatch.Stop();

                _log.Information($"LB.PhotoGalleries.Worker.Program.ProcessImageAsync() - Delete blob elapsed time: {deleteStopwatch.ElapsedMilliseconds}ms. Upload blob elapsed time: {uploadStopwatch.ElapsedMilliseconds}ms");
            }

            // update the Image object with the storage id of the newly-generated image file
            switch (fileSpec)
            {
                case FileSpec.Spec3840:
                    image.Files.Spec3840Id = storageId;
                    break;
                case FileSpec.Spec2560:
                    image.Files.Spec2560Id = storageId;
                    break;
                case FileSpec.Spec1920:
                    image.Files.Spec1920Id = storageId;
                    break;
                case FileSpec.Spec800:
                    image.Files.Spec800Id = storageId;
                    break;
                case FileSpec.SpecLowRes:
                    image.Files.SpecLowResId = storageId;
                    break;
            }
        }

        /// <summary>
        /// Generates an resized image of the original in WebP format (quicker, smaller files).
        /// </summary>
        /// <param name="image">The Image the new file should be generated for. Used for logging purposes.</param>
        /// <param name="originalImage">The byte array for the original image.</param>
        /// <param name="imageFileSpec">The name of the image file specification to base the new image.</param>
        /// <returns>A new image stream for the resized image</returns>
        private static async Task<Stream> GenerateImageAsync(Image image, byte[] originalImage, ImageFileSpec imageFileSpec)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            using (var job = new ImageJob())
            {
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

                    stopwatch.Stop();
                    _log.Information($"LB.PhotoGalleries.Worker.Program.GenerateImageAsync() - Image {image.Id} and spec {imageFileSpec.FileSpec} done. Elapsed time: {stopwatch.ElapsedMilliseconds}ms");
                    return newStream;
                }
            }

            stopwatch.Stop();
            _log.Warning($"LB.PhotoGalleries.Worker.Program.GenerateImageAsync() - Couldn't generate new image for {image.Id}! Elapsed time: {stopwatch.ElapsedMilliseconds}ms");
            return null;
        }

        private static async Task UpdateModelsAsync(Image image)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // update Image in the db
            var replaceResult = await _imagesContainer.ReplaceItemAsync(image, image.Id, new PartitionKey(image.GalleryId));
            _log.Information($"LB.PhotoGalleries.Worker.Program.UpdateModelsAsync() - Replace Image response: {replaceResult.StatusCode}. Charge: {replaceResult.RequestCharge}");

            // update the gallery thumbnail if this is the first image being added to the gallery
            var galleryContainer = _database.GetContainer(Constants.GalleriesContainerName);
            var getGalleryResponse = await galleryContainer.ReadItemAsync<Gallery>(image.GalleryId, new PartitionKey(image.GalleryCategoryId));
            _log.Information($"LB.PhotoGalleries.Worker.Program.UpdateModelsAsync() - Get gallery request charge: {getGalleryResponse.RequestCharge}");
            var gallery = getGalleryResponse.Resource;

            if (string.IsNullOrEmpty(gallery.ThumbnailStorageId))
            {
                // todo: change this so we write the whole Files property to the gallery so we can choose high-res versions as needed
                gallery.ThumbnailStorageId = image.Files.Spec800Id;

                // update the gallery in the db
                var updateGalleryResponse = await galleryContainer.ReplaceItemAsync(gallery, gallery.Id, new PartitionKey(gallery.CategoryId));
                _log.Information($"LB.PhotoGalleries.Worker.Program.UpdateModelsAsync() - First image, setting gallery thumbnail. galleryId {gallery.Id}, galleryCategoryId {gallery.CategoryId}. Request charge: {updateGalleryResponse.RequestCharge}");
            }

            stopwatch.Stop();
            _log.Information($"LB.PhotoGalleries.Worker.Program.UpdateModelsAsync() - Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

            // todo: in the future: expire Image cache item when we implement domain caching
        }
        #endregion
    }
}
