using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Imageflow.Fluent;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Models.Exceptions;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
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
        private static QueueClient _queueClient;
        private static CosmosClient _cosmosClient;
        private static BlobServiceClient _blobServiceClient;
        private static Database _database;
        private static Container _imagesContainer;
        private static Container _galleriesContainer;
        private static ILogger _log;
        private static DatabaseId _galleryId;
        #endregion

        private static async Task Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No configuration filename argument supplied. Cannot continue.");
                return;
            }

            InitialiseConfiguration(args);
            InitialiseLogging();

            try
            {
                // setup clients/references
                _cosmosClient = new CosmosClient(_configuration["CosmosDB:Uri"], _configuration["CosmosDB:PrimaryKey"]);
                _blobServiceClient = new BlobServiceClient(_configuration["Storage:ConnectionString"]);
                _database = _cosmosClient.GetDatabase(_configuration["CosmosDB:DatabaseName"]);
                _imagesContainer = _database.GetContainer(Constants.ImagesContainerName);
                _galleriesContainer = _database.GetContainer(Constants.GalleriesContainerName);
                _galleryId = new DatabaseId();

                // set the message queue listener
                var queueName = _configuration["Storage:ImageProcessingQueueName"];
                _queueClient = new QueueClient(_configuration["Storage:ConnectionString"], queueName);
                int.TryParse(_configuration["Storage:MessageBatchSize"], out var messageBatchSize);
                int.TryParse(_configuration["Storage:MessageBatchVisibilityTimeoutMins"], out var messageBatchVisibilityMins);
                if (!await _queueClient.ExistsAsync())
                {
                    _log.Fatal($"LB.PhotoGalleries.Worker.Program.Main() - {queueName} queue does not exist. Cannot continue.");
                    return;
                }

                if (!int.TryParse(_configuration["ZeroMessagesPollIntervalSeconds"], out var zeroMessagesPollIntervalSeconds))
                    zeroMessagesPollIntervalSeconds = 5;
                var delayTime = TimeSpan.FromSeconds(zeroMessagesPollIntervalSeconds);
                var zeroMessagesCount = 0;

                // keep processing the queue until the program is shutdown
                while (true)
                {
                    // get a batch of messages from the queue to process
                    // getting a batch is more efficient as it minimises the number of HTTP calls we have to make to the queue
                    var messages = await _queueClient.ReceiveMessagesAsync(messageBatchSize, TimeSpan.FromMinutes(messageBatchVisibilityMins));
                    if (zeroMessagesCount < 2)
                        _log.Information($"LB.PhotoGalleries.Worker.Program.Main() - Received {messages.Value.Length} messages from the {queueName} queue ");

                    if (messages.Value.Length > 0)
                    {
                        // this is the fastest method of processing messages I have found so far. It's wrong I know to use async and block, but numbers don't lie.
                        Parallel.ForEach(messages.Value, message => {
                            HandleMessageAsync(message).GetAwaiter().GetResult();
                        });

                        // assign a gallery thumbnail if one is missing
                        await AssignGalleryThumbnailAsync();
                    }

                    // if we we received messages this iteration then there's a good chance there's more to process so don't pause between polls
                    // otherwise limit the rate we poll the queue and also don't log messages after a while
                    if (messages.Value.Length == 0)
                    {
                        if (zeroMessagesCount == 2)
                            _log.Information($"Stopping logging until we we receive messages again. Still polling the queue every {delayTime} seconds though");

                        zeroMessagesCount += 1;
                        await Task.Delay(delayTime);
                    }
                    else
                    {
                        zeroMessagesCount = 0;
                    }
                }
            }
            catch (Exception exception)
            {
                _log.Fatal(exception, "LB.PhotoGalleries.Worker.Program.Main() - Unhandled exception!");
            }
        }

        #region initialisation methods
        private static void InitialiseConfiguration(string[] args)
        {
            _configuration = new ConfigurationBuilder()
                .AddJsonFile(args[0], optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
        }

        private static void InitialiseLogging()
        {
            var loggerConfiguration = new LoggerConfiguration();
            var loggingMinimumLevel = _configuration["Logging:MinimumLevel"];
            switch (loggingMinimumLevel)
            {
                case "Verbose":
                    loggerConfiguration.MinimumLevel.Verbose();
                    break;
                case "Debug":
                    loggerConfiguration.MinimumLevel.Debug();
                    break;
                case "Information":
                    loggerConfiguration.MinimumLevel.Information();
                    break;
                case "Warning":
                    loggerConfiguration.MinimumLevel.Warning();
                    break;
                case "Error":
                    loggerConfiguration.MinimumLevel.Error();
                    break;
                case "Fatal":
                    loggerConfiguration.MinimumLevel.Fatal();
                    break;
            }

            loggerConfiguration.WriteTo.File(Path.Combine(_configuration["Logging:Path"], "lb.photogalleries.worker.log"), rollingInterval: RollingInterval.Day);
            loggerConfiguration.WriteTo.Console();
            loggerConfiguration.WriteTo.ApplicationInsights(new TelemetryConfiguration(_configuration["ApplicationInsights:InstrumentationKey"]), TelemetryConverter.Traces);
            _log = loggerConfiguration.CreateLogger();
            _log.Information("Starting worker...");
        }
        #endregion

        #region image processing methods
        private static async Task HandleMessageAsync(QueueMessage message)
        {
            // decode the message
            var components = Utilities.Base64Decode(message.MessageText).Split(':');
            ImageMessage imageMessage = null;
            try
            {
                imageMessage = new ImageMessage
                {
                    Operation = Enum.Parse<WorkerOperation>(components[0], true),
                    ImageId = components[1],
                    GalleryId = components[2],
                    GalleryCategoryId = components[3],
                    OverwriteImageProperties = bool.Parse(components[4])
                };
            }
            catch (Exception e)
            {
                Log.Error($"LB.PhotoGalleries.Worker.Program.HandleMessageAsync() - Failed to decode message : '{message.MessageText}'", e);
            }

            try
            {
                if (imageMessage != null)
                {
                    if (imageMessage.Operation == WorkerOperation.Process)
                        await ProcessImageProcessingMessageAsync(imageMessage);
                    else
                        await ReprocessImageMetadataAsync(imageMessage);
                }
                
            }
            catch (ImageNotFoundException e)
            {
                _log.Error(e, "Image not found, deleting message.");
            }

            // as the message was processed successfully, we can delete the message from the queue
            await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
        }

        private static async Task ProcessImageProcessingMessageAsync(ImageMessage message)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // keep track of the gallery id so we can work on the gallery after the message batch is processed
            lock (_galleryId)
                _galleryId = new DatabaseId(message.GalleryId, message.GalleryCategoryId);

            // retrieve Image object and bytes
            var image = await GetImageAsync(message.ImageId, message.GalleryId);
            var imageBytes = await GetImageBytesAsync(image);

            // create array of file specs to iterate over
            var specs = new List<FileSpec> { FileSpec.Spec3840, FileSpec.Spec2560, FileSpec.Spec1920, FileSpec.Spec800, FileSpec.SpecLowRes };

            // resize original image file to smaller versions in parallel
            //var parallelTasks = specs.Select(spec => ProcessImageAsync(image, imageBytes, spec)).ToList();
            //await Task.WhenAll(parallelTasks);

            //foreach (var spec in specs)
            //    await ProcessImageAsync(image, imageBytes, spec);

            // This approach is the fastest of the three methods here. I have no idea why.
            // 20MB image resized five times (each spec)
            // Task.WhenAll: 11785ms average
            // Foreach: 8540ms average
            // Parallel.Foreach with GetAwaiter/GetResult: 7887ms average
            Parallel.ForEach(specs, spec =>
            {
                ProcessImageAsync(image, imageBytes, spec).GetAwaiter().GetResult();
            });

            MetadataUtils.ParseAndAssignImageMetadata(image, imageBytes, message.OverwriteImageProperties, _log);
            await UpdateImageAsync(image);

            // when uploading images, they don't have a position set, so if one is set when we process it here
            // then it's likely it's an existing Image that's having it's image file replaced. If so and the position
            // is zero then we want to make sure we update the gallery thumbnail to use the new image files.
            if (image.Position == 0)
            {
                _log.Debug("LB.PhotoGalleries.Worker.Program.ProcessImageProcessingMessageAsync() - Image position = 0, need to update gallery thumbnail...");
                var gallery = await GetGalleryAsync(image.GalleryCategoryId, image.GalleryId);
                await UpdateGalleryThumbnailAsync(gallery, image.Files);
            }

            stopwatch.Stop();
            _log.Information($"LB.PhotoGalleries.Worker.Program.ProcessImageProcessingMessageAsync() - Processed {image.Id} in {stopwatch.ElapsedMilliseconds}ms");
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
                // upload the new image file to storage
                var containerClient = _blobServiceClient.GetBlobContainerClient(imageFileSpec.ContainerName);
                var uploadStopwatch = new Stopwatch();
                uploadStopwatch.Start();
                await containerClient.UploadBlobAsync(storageId, imageFile);
                uploadStopwatch.Stop();

                _log.Information($"LB.PhotoGalleries.Worker.Program.ProcessImageAsync() - Upload blob elapsed time: {uploadStopwatch.ElapsedMilliseconds}ms");
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
                var buildNode = job.Decode(originalImage);
                var resampleHints = new ResampleHints();

                if (imageFileSpec.FileSpecFormat != FileSpecFormat.WebPLossless && imageFileSpec.SharpeningAmount > 0)
                    resampleHints.SetSharpen(imageFileSpec.SharpeningAmount, SharpenWhen.Downscaling).SetResampleFilters(imageFileSpec.InterpolationFilter, null);

                buildNode = buildNode.ConstrainWithin(imageFileSpec.PixelLength, imageFileSpec.PixelLength, resampleHints);
                IEncoderPreset encoderPreset;

                if (imageFileSpec.FileSpecFormat == FileSpecFormat.WebPLossless)
                    encoderPreset = new WebPLosslessEncoder();
                else if (imageFileSpec.FileSpecFormat == FileSpecFormat.WebPLossy)
                    encoderPreset = new WebPLossyEncoder(imageFileSpec.Quality);
                else
                    encoderPreset = new MozJpegEncoder(imageFileSpec.Quality, true);

                var result = await buildNode
                    .EncodeToBytes(encoderPreset)
                    .Finish()
                    .SetSecurityOptions(new SecurityOptions()
                        .SetMaxDecodeSize(new FrameSizeLimit(99999, 99999, 200))
                        .SetMaxFrameSize(new FrameSizeLimit(99999, 99999, 200))
                        .SetMaxEncodeSize(new FrameSizeLimit(99999, 99999, 200)))
                    .InProcessAsync();

                var newImageBytes = result.First.TryGetBytes();
                if (newImageBytes.HasValue)
                {
                    var newStream = new MemoryStream(newImageBytes.Value.ToArray());

                    stopwatch.Stop();
                    _log.Information($"LB.PhotoGalleries.Worker.Program.GenerateImageAsync() - Image {image.Id} and spec {imageFileSpec} done. Elapsed time: {stopwatch.ElapsedMilliseconds}ms");
                    return newStream;
                }
            }

            stopwatch.Stop();
            _log.Warning($"LB.PhotoGalleries.Worker.Program.GenerateImageAsync() - Couldn't generate new image for {image.Id}! Elapsed time: {stopwatch.ElapsedMilliseconds}ms");
            return null;
        }

        /// <summary>
        /// Galleries need a copy of the first image's ImageFiles object so it can efficiently render high-resolution thumbnails without
        /// making sub-queries for this data. This method finds the first gallery image thumbnail data and updates the gallery with it.
        /// </summary>
        private static async Task AssignGalleryThumbnailAsync()
        {
            if (_galleryId == null || !_galleryId.PartitionKey.HasValue() || !_galleryId.Id.HasValue())
            {
                _log.Information("AssignGalleryThumbnailAsync() - Exiting, no ids. Probably due to Reprocessing operations");
                return;
            }

            var g = await GetGalleryAsync(_galleryId.PartitionKey, _galleryId.Id);
            if (g.ThumbnailFiles == null)
            {
                // get the first image:
                // try and get where position = 0 first
                // if no results, then get where date created is earliest
                // check if the files have been generated, if not then exit and wait until this is run as part of another batch of messages

                var query = "SELECT TOP 1 * FROM i WHERE i.GalleryId = @galleryId AND i.Position = 0";
                var queryDefinition = new QueryDefinition(query).WithParameter("@galleryId", g.Id);
                var queryResult = _imagesContainer.GetItemQueryIterator<Image>(queryDefinition);
                ImageFiles imageFiles = null;

                while (queryResult.HasMoreResults)
                {
                    var queryResponse = await queryResult.ReadNextAsync();
                    _log.Information($"LB.PhotoGalleries.Worker.Program.AssignGalleryThumbnailAsync() - Position query charge: {queryResponse.RequestCharge}. GalleryId: {g.Id}");

                    foreach (var item in queryResponse.Resource)
                    {
                        if (string.IsNullOrEmpty(item.Files.SpecLowResId))
                        {
                            // this image should be the thumbnail, but it hasn't had it's image files processed yet, so exit for now
                            // and we'll pick it up in a subsequent message batch processing hopefully.
                            _log.Information($"LB.PhotoGalleries.Worker.Program.AssignGalleryThumbnailAsync() - Thumbnail image found, but it hasn't been processed yet, skipping for now. GalleryId: {g.Id}");
                            return;
                        }

                        imageFiles = item.Files;
                        _log.Verbose($"LB.PhotoGalleries.Worker.Program.AssignGalleryThumbnailAsync() - Got thumbnail image via Position query. GalleryId: {g.Id}");
                        break;
                    }
                }

                if (imageFiles == null)
                {
                    // no position value set on images, get first image created
                    query = "SELECT TOP 1 * FROM i WHERE i.GalleryId = @galleryId AND NOT IS_NULL(i.Files.SpecLowResId) ORDER BY i.Created";
                    queryDefinition = new QueryDefinition(query).WithParameter("@galleryId", g.Id);
                    queryResult = _imagesContainer.GetItemQueryIterator<Image>(queryDefinition);

                    while (queryResult.HasMoreResults)
                    {
                        var queryResponse = await queryResult.ReadNextAsync();
                        _log.Information($"LB.PhotoGalleries.Worker.Program.AssignGalleryThumbnailAsync() - Date query charge: {queryResponse.RequestCharge} GalleryId: {g.Id}");

                        foreach (var item in queryResponse.Resource)
                        {
                            imageFiles = item.Files;
                            _log.Verbose($"LB.PhotoGalleries.Worker.Program.AssignGalleryThumbnailAsync() - Got thumbnail image via date query. GalleryId: {g.Id}");
                            break;
                        }
                    }
                }

                if (imageFiles == null)
                {
                    _log.Information($"LB.PhotoGalleries.Worker.Program.AssignGalleryThumbnailAsync() - Couldn't retrieve first image in gallery. imageFiles is null. GalleryID: {g.Id}");
                    return;
                }

                await UpdateGalleryThumbnailAsync(g, imageFiles);
            }
            else
            {
                _log.Information($"LB.PhotoGalleries.Worker.Program.AssignGalleryThumbnailAsync() - Gallery ThumbnailFiles already set on gallery {g.Id}");
            }
        }

        /// <summary>
        /// Updates the Gallery object with the new imageFiles for the thumbnail.
        /// </summary>
        private static async Task UpdateGalleryThumbnailAsync(Gallery gallery, ImageFiles imageFiles)
        {
            gallery.ThumbnailFiles = imageFiles;
            var replaceResponse = await _galleriesContainer.ReplaceItemAsync(gallery, gallery.Id, new PartitionKey(gallery.CategoryId));
            _log.Information($"LB.PhotoGalleries.Worker.Program.UpdateGalleryThumbnailAsync() - Gallery thumbnail updated. Charge: {replaceResponse.RequestCharge}");
        }
        #endregion

        #region reprocessing methods
        /// <summary>
        /// Causes an image to have it's metadata re-examined and updates applied to the image.
        /// Useful for when we make improvements to metadata parsing.
        /// </summary>
        private static async Task ReprocessImageMetadataAsync(ImageMessage message)
        {
            // retrieve the image
            // download the bytes
            // parse the metadata
            // update the image properties
            // persist image changes to db

            _log.Verbose("LB.PhotoGalleries.Worker.Program.ReprocessImageMetadataAsync()");
            
            var image = await GetImageAsync(message.ImageId, message.GalleryId);
            var imageBytes = await GetImageBytesAsync(image);
            MetadataUtils.ParseAndAssignImageMetadata(image, imageBytes, message.OverwriteImageProperties);
            await UpdateImageAsync(image);
            
            _log.Information($"LB.PhotoGalleries.Worker.Program.ReprocessImageMetadataAsync() - Reprocessed metadata on image {image.Id}");
        }
        #endregion

        #region utility methods
        private static async Task<Gallery> GetGalleryAsync(string categoryId, string galleryId)
        {
            var readItemResponse = await _galleriesContainer.ReadItemAsync<Gallery>(galleryId, new PartitionKey(categoryId));
            return readItemResponse.Resource;
        }

        /// <summary>
        /// After creating various smaller image files we need to update the database with the new filenames.
        /// </summary>
        private static async Task UpdateImageAsync(Image image)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // update Image in the db with the new Files references we've created
            var replaceResult = await _imagesContainer.ReplaceItemAsync(image, image.Id, new PartitionKey(image.GalleryId));
            _log.Information($"LB.PhotoGalleries.Worker.Program.UpdateImageAsync() - Replace Image response: {replaceResult.StatusCode}. Charge: {replaceResult.RequestCharge}");

            stopwatch.Stop();
            _log.Information($"LB.PhotoGalleries.Worker.Program.UpdateImageAsync() - Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

            // todo: in the future: expire Image cache item when we implement domain caching
        }

        private static async Task<Image> GetImageAsync(string imageId, string galleryId)
        {
            // it's possible that we've picked this message up so quick after the Image was created that Cosmos DB replication hasn't had a chance to make
            // sure the new record is fully available
            var getImageTries = 0;
            while (getImageTries < 5)
            {
                try
                {
                    var response = await _imagesContainer.ReadItemAsync<Image>(imageId, new PartitionKey(galleryId));
                    if (response.StatusCode == HttpStatusCode.OK)
                        return response.Resource;
                }
                catch (CosmosException e)
                {
                    if (e.StatusCode == HttpStatusCode.NotFound)
                    {
                        _log.Warning($"LB.PhotoGalleries.Worker.Program.GetImage() - Image {imageId} could not be retrieved from CosmosDB. StatusCode: {e.StatusCode}");
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                }

                getImageTries++;
            }

            // this can happen if the user deletes the photo before the message is processed. Raise a specific exception so we can handle it gracefully.
            throw new ImageNotFoundException($"Didn't find image {imageId} in the database.") {ImageId = imageId, GalleryId = galleryId};
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
            _log.Information($"LB.PhotoGalleries.Worker.Program.GetImageBytesAsync() - Image ({imageBytes.Length/1024}kb) downloaded in: {downloadTimer.ElapsedMilliseconds}ms");

            return imageBytes;
        }
        #endregion
    }
}