using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Application.Servers;
using LB.PhotoGalleries.Models;
using Microsoft.AspNetCore.Authentication;
using Serilog;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LB.PhotoGalleries;

public static class UserManagement
{
    public static async Task UpdateUserFromClaimsAsync(TicketReceivedContext context)
    {
        Log.Debug("UpdateUserFromClaimsAsync()");
        var userId = context.Principal.FindFirstValue("sub");
        var user = await Server.Instance.Users.GetUserAsync(userId);
        var updateNeeded = false;

        if (user == null)
        {
            // the user is new, create them
            user = new User
            {
                Id = context.Principal.FindFirstValue("sub"),
                Name = context.Principal.FindFirstValue("name"),
                Email = context.Principal.FindFirstValue("email"),
                // Picture is only set once the picture has been downloaded, below
                LegacyApolloId = context.Principal.FindFirstValue("urn:londonbikers:legacyapolloid")
            };

            // set any defaults
            user.CommunicationPreferences.ReceiveCommentNotifications = true;
            updateNeeded = true;
        }
        else
        {
            // we already have an existing user for them, update their attributes if necessary
            // string.Equals rather than user.Name.Equals, as older or migrated users can have no name or email stored
            if (!string.Equals(user.Name, context.Principal.FindFirstValue("name"), StringComparison.OrdinalIgnoreCase))
            {
                user.Name = context.Principal.FindFirstValue("name");
                updateNeeded = true;
            }

            if (!string.Equals(user.Email, context.Principal.FindFirstValue("email"), StringComparison.OrdinalIgnoreCase))
            {
                user.Email = context.Principal.FindFirstValue("email");
                updateNeeded = true;
            }
        }

        // download their IDP-provided profile picture they're new or it's been updated
        var pictureClaimValue = context.Principal.FindFirstValue("picture");

        // only download the picture if we don't have it yet or it's changed, and not if an admin has removed it
        if (UserServer.NeedsPictureDownload(user, pictureClaimValue))
        {
            Log.Debug("UpdateUserFromClaimsAsync(): About to download picture");
            var previousPictureHostedUrl = user.PictureHostedUrl;
            await Server.Instance.Users.DownloadAndStoreUserPictureAsync(user, pictureClaimValue);

            // the download can fail, i.e. the picture isn't https, in which case there's nothing to save
            if (user.PictureHostedUrl != previousPictureHostedUrl)
            {
                updateNeeded = true;
                Log.Debug("UpdateUserFromClaimsAsync(): Downloaded picture");
            }
        }

        if (updateNeeded)
        {
            // we'll either create them or update them, which is useful if their
            // profile picture has changed from their source identity provider, i.e. Facebook
            await Server.Instance.Users.CreateOrUpdateUserAsync(user);
            Log.Debug("UpdateUserFromClaimsAsync(): Updated user");
        }
    }
}