using LB.PhotoGalleries.Application.Models;
using System.Collections.Generic;
using System.Threading;

namespace LB.PhotoGalleries.Application
{
    /// <summary>
    /// Provides an in-process processing queue to pre-generate image files.
    /// </summary>
    /// <remarks>
    /// Designed to allow low-specification hosts to work through a queue of Images on a background thread that need their pre-generated images created one at a time without saturating the server.
    /// Implemented using a non-dedicated thread as the queue will be idle most of the time.
    /// Previously we have tried creating images in parallel using a fire-and-forget approach but this saturated the Azure hosts which are low-spec (at our budget).
    /// We could just fire-and-forget the tasks synchronously to avoid saturating the server, but this way we get to query the queue to see how much work is outstanding, which helps with understanding server resourcing needs.
    /// Foundation comes from: https://michaelscodingspot.com/c-job-queues-with-reactive-extensions-and-channels/
    /// </remarks>
    public class PreGenImagesQueue
    {
        #region members
        private readonly Queue<PreGenImagesJob> _jobs = new Queue<PreGenImagesJob>();
        private bool _delegateQueuedOrRunning;
        #endregion

        #region accessors
        public int Count => _jobs.Count;
        #endregion

        #region internal methods
        internal void Enqueue(PreGenImagesJob job)
        {
            lock (_jobs)
            {
                _jobs.Enqueue(job);
                if (_delegateQueuedOrRunning) 
                    return;

                _delegateQueuedOrRunning = true;
                ThreadPool.UnsafeQueueUserWorkItem(ProcessQueuedItems, null);
            }
        }
        #endregion

        #region private methods
        private void ProcessQueuedItems(object ignored)
        {
            while (true)
            {
                PreGenImagesJob item;
                lock (_jobs)
                {
                    if (_jobs.Count == 0)
                    {
                        _delegateQueuedOrRunning = false;
                        break;
                    }

                    item = _jobs.Dequeue();
                }

                try
                {
                    //do job
                    Server.Instance.Images.GenerateFilesAndUpdateImageAsync(item.Image, item.ImageBytes).GetAwaiter().GetResult();
                }
                catch
                {
                    ThreadPool.UnsafeQueueUserWorkItem(ProcessQueuedItems, null);
                    throw;
                }
            }
        }
        #endregion
    }
}
