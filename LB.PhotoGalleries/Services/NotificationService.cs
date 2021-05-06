using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Shared;
using Mailjet.Client;
using Mailjet.Client.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Services
{
    public class NotificationService : BackgroundService
    {
        #region members
        private static IConfiguration _configuration;
        private readonly ILogger<NotificationService> _log;
        private static QueueClient _queueClient;
        #endregion

        #region constructors
        public NotificationService(IConfiguration configuration, ILogger<NotificationService> log)
        {
            _configuration = configuration;
            _log = log;
        }
        #endregion

        #region overrides
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogDebug("LB.PhotoGalleries.Services.NotificationService.ExecuteAsync() - Starting");
            stoppingToken.Register(() => _log.LogDebug("NotificationService background task is stopping."));

            // set the message queue listener
            var queueName = _configuration["Services:Notifications:QueueName"];
            _queueClient = new QueueClient(_configuration["Storage:ConnectionString"], queueName);
            int.TryParse(_configuration["Services:Notifications:MessageBatchSize"], out var messageBatchSize);
            int.TryParse(_configuration["Services:Notifications:MessageBatchVisibilityTimeoutMins"], out var messageBatchVisibilityMins);

            if (!await _queueClient.ExistsAsync(stoppingToken))
            {
                _log.LogCritical($"LB.PhotoGalleries.Services.NotificationService.ExecuteAsync() - {queueName} queue does not exist. Cannot continue.");
                return;
            }

            if (!int.TryParse(_configuration["Services:Notifications:ZeroMessagesPollIntervalSeconds"], out var zeroMessagesPollIntervalSeconds))
                zeroMessagesPollIntervalSeconds = 5;
            var delayTime = TimeSpan.FromSeconds(zeroMessagesPollIntervalSeconds);
            var zeroMessagesCount = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                _log.LogDebug("LB.PhotoGalleries.Services.NotificationService task doing background work.");

                // get a batch of messages from the queue to process
                // getting a batch is more efficient as it minimises the number of HTTP calls we have to make to the queue
                var messages = await _queueClient.ReceiveMessagesAsync(messageBatchSize, TimeSpan.FromMinutes(messageBatchVisibilityMins), stoppingToken);
                if (zeroMessagesCount < 2)
                    _log.LogInformation($"NotificationService.ExecuteAsync() - Received {messages.Value.Length} messages from the {queueName} queue ");

                if (messages.Value.Length > 0)
                {
                    foreach (var message in messages.Value)
                    {
                        await HandleMessageAsync(message);
                    }
                }

                // if we we received messages this iteration then there's a good chance there's more to process so don't pause between polls
                // otherwise limit the rate we poll the queue and also don't log messages after a while
                if (messages.Value.Length == 0)
                {
                    if (zeroMessagesCount == 2)
                        _log.LogInformation($"NotificationService.ExecuteAsync() - Stopping logging until we we receive messages again. Still polling the queue every {delayTime} seconds though");

                    zeroMessagesCount += 1;
                    await Task.Delay(delayTime, stoppingToken);
                }
                else
                {
                    zeroMessagesCount = 0;
                }
            }

            _log.LogDebug("LB.PhotoGalleries.Services.NotificationService.ExecuteAsync() - Stopping");
        }
        #endregion

        #region private methods
        private async Task HandleMessageAsync(QueueMessage message)
        {
            // message content format:
            // - object primary id
            // - object secondary id
            // - object type
            // - when the author commented
            // i.e. 135,image,01/01/2021 18:54:00

            var messageParts = Utilities.Base64Decode(message.MessageText).Split(':');
            if (messageParts.Length != 4)
            {
                _log.LogError($"Message did not have three parts as expected: '{message.MessageText}'");
            }
            else
            {
                var commentObjectId1 = messageParts[0];
                var commentObjectId2 = messageParts[1];
                var commentObjectType = messageParts[2];
                var ticks = long.Parse(messageParts[3]);
                var commentCreated = new DateTime(ticks);

                // get the object
                // get the comment
                // get the user
                // get all notification subscriptions, enumerate
                //  build the email
                //  send the email

                Comment comment = null;
                List<string> userCommentSubscriptions = null;
                string emailSubjectObjectType = null;
                string commentObjectName = null;
                string commentObjectHref = null;
                switch (commentObjectType)
                {
                    case "image":
                    {
                        var image = await Server.Instance.Images.GetImageAsync(commentObjectId1, commentObjectId2);
                        comment = image.Comments.Single(q => q.Created == commentCreated);
                        userCommentSubscriptions = image.UserCommentSubscriptions;
                        emailSubjectObjectType = "Photo";
                        commentObjectName = image.Name;
                        commentObjectHref = Helpers.GetFullImageUrl(_configuration, image, comment.Created) + $"#comments?ct={commentCreated.Ticks}";
                        break;
                    }
                    case "gallery":
                    {
                        var gallery = await Server.Instance.Galleries.GetGalleryAsync(commentObjectId1, commentObjectId2);
                        comment = gallery.Comments.Single(q => q.Created == commentCreated);
                        userCommentSubscriptions = gallery.UserCommentSubscriptions;
                        emailSubjectObjectType = "Gallery";
                        commentObjectName = gallery.Name;
                        commentObjectHref = Helpers.GetFullGalleryUrl(_configuration, gallery, comment.Created) + $"#comments?ct={commentCreated.Ticks}";
                        break;
                    }
                }

                if (comment != null)
                {
                    // setup email vars
                    var isDevelopment = true;
                    if (!string.IsNullOrEmpty(_configuration["IsDevelopment"]))
                        isDevelopment = bool.Parse(_configuration["IsDevelopment"]);

                    var developmentEmailAddress = _configuration["DevelopmentRedirectEmailAddress"];
                    var clientId = _configuration["Mailjet:ClientId"];
                    var secret = _configuration["Mailjet:Secret"];
                    var fromAddress = _configuration["Mailjet:FromAddress"];
                    var fromName = _configuration["Mailjet:FromLabel"];
                    var newCommentEmailTemplateId = _configuration["Services:Notifications:NewCommentEmailTemplateId"];
                    var client = new MailjetClient(clientId, secret);

                    // enumerate the comment subscriptions. don't include the comment author!
                    var commentUser = await Server.Instance.Users.GetUserAsync(comment.CreatedByUserId);
                    foreach (var subscriptionUserId in userCommentSubscriptions.Where(id=> id != commentUser.Id))
                    {
                        var subscriptionUser = await Server.Instance.Users.GetUserAsync(subscriptionUserId);

                        // if we're in development mode, do not send emails to any real members. that would be bad.
                        // instead, redirect all outgoing mail to the test mailbox.
                        var toEmail = subscriptionUser.Email;
                        if (isDevelopment)
                            toEmail = developmentEmailAddress;

                        // start building the email
                        var request = new MailjetRequest  { Resource = Send.Resource }
                            .Property(Send.MjTemplateID, newCommentEmailTemplateId)
                            .Property(Send.MjTemplateLanguage, "True")
                            .Property(Send.FromEmail, fromAddress)
                            .Property(Send.FromName, fromName)
                            .Property(Send.Subject, $"{emailSubjectObjectType} comment notification")
                            .Property(Send.Recipients, new JArray {
                                    new JObject {
                                        {"Email", toEmail},
                                        {"Name", subscriptionUser.Name}
                                    }
                                })
                            .Property(Send.Vars, new JObject {
                                {"recipient_username", subscriptionUser.Name},
                                {"comment_username", commentUser.Name},
                                {"comment_object_type", emailSubjectObjectType },
                                {"comment_object_name", commentObjectName},
                                {"comment_object_href", commentObjectHref},
                                {"current_year", DateTime.Now.Year }
                            });

                        // send the email
                        var response = await client.PostAsync(request);
                        if (response.IsSuccessStatusCode) continue;

                        // try again a few times, in case there's a networking issue
                        var tries = 0;
                        while (tries < 5)
                        {
                            response = await client.PostAsync(request);
                            if (response.IsSuccessStatusCode)
                                break;

                            _log.LogWarning($"Did not receive a success status code from Mailjet: {response.StatusCode} - {response.GetErrorMessage()} - {response.GetErrorInfo()}");
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                            tries++;
                        }

                        if (!response.IsSuccessStatusCode)
                            _log.LogError("Failed to send email via Mailjet. Giving up.");
                    }
                }

                // as the message was processed successfully, we can delete the message from the queue
                await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
            }
        }
        #endregion
    }
}
