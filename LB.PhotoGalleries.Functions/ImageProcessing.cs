using LB.PhotoGalleries.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Functions
{
    public static class ImageProcessing
    {
        [FunctionName("ImageProcessingQueueStart")]
        public static Task Run(
            [QueueTrigger("images-to-process", Connection = "Storage:ConnectionString")] string queueMessage,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            // Orchestration queueMessage comes from the queue message content.
            return starter.StartNewAsync("ImageProcessingOrchestrator", queueMessage);
        }

        [FunctionName("ImageProcessingOrchestrator")]
        public static async Task RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            //var imageId = context.GetInput<string>();
            var imageId = context.InstanceId;
            var imageBytes = new byte[0];

            log.LogDebug("ImageProcessingOrchestrator() - imageId: " + imageId);

            // download bytes
            // create array of file specs
            // resize images in parallel
            // update image in db
            // in future: expire Image cache item

            var parallelTasks = new List<Task<ProcessImageResponse>>();

            var t1 = context.CallActivityAsync<ProcessImageResponse>("ProcessImage", new ProcessImageInput(imageId, imageBytes, "Spec1"));
            var t2 = context.CallActivityAsync<ProcessImageResponse>("ProcessImage", new ProcessImageInput(imageId, imageBytes, "Spec2"));
            var t3 = context.CallActivityAsync<ProcessImageResponse>("ProcessImage", new ProcessImageInput(imageId, imageBytes, "Spec3"));
            var t4 = context.CallActivityAsync<ProcessImageResponse>("ProcessImage", new ProcessImageInput(imageId, imageBytes, "Spec4"));

            parallelTasks.Add(t1);
            parallelTasks.Add(t2);
            parallelTasks.Add(t3);
            parallelTasks.Add(t4);

            await Task.WhenAll(parallelTasks);

            foreach (var t in parallelTasks)
            {
                log.LogInformation($"processing complete: filespec = {t.Result.FileSpec}, storageid = {t.Result.StorageId}");
            }
        }

        /// <summary>
        /// Produces a new image file, stores it returns the storage id.
        /// </summary>
        /// <param name="input">The input object containing what we need to process the image.</param>
        /// <param name="log">The logging interface.</param>
        /// <returns>The storage id of the resized image, i.e. 465ds4f5d4d54ds465we.webp.</returns>
        [FunctionName("ProcessImage")]
        public static ProcessImageResponse ProcessImage([ActivityTrigger] ProcessImageInput input, ILogger log)
        {
            log.LogInformation($"Processing image {input.ImageId} for file spec {input.FileSpec}...");
            var newId = Guid.NewGuid().ToString().Replace("-", string.Empty) + "webp";
            return new ProcessImageResponse(input.FileSpec, newId);
        }
    }
}
