using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Authentication;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LB.PhotoGalleries
{
    public static class UserManagement
    {
        public static async Task UpdateUserFromClaimsAsync(TicketReceivedContext context)
        {
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
                    Picture = context.Principal.FindFirstValue("picture"),
                    LegacyApolloId = context.Principal.FindFirstValue("urn:londonbikers:legacyapolloid")
                };

                // set any defaults
                user.CommunicationPreferences.ReceiveCommentNotifications = true;

                updateNeeded = true;
            }
            else
            {
                // we already have an existing user for them, update their attributes if necessary
                if (!user.Name.Equals(context.Principal.FindFirstValue("name"), StringComparison.CurrentCultureIgnoreCase))
                {
                    user.Name = context.Principal.FindFirstValue("name");
                    updateNeeded = true;
                }

                if (!user.Email.Equals(context.Principal.FindFirstValue("email"), StringComparison.CurrentCultureIgnoreCase))
                {
                    user.Email = context.Principal.FindFirstValue("email");
                    updateNeeded = true;
                }
            }

            // download their IDP-provided profile picture they're new or it's been updated
            var pictureClaimValue = context.Principal.FindFirstValue("picture");

            // only update the picture if we have an inbound claim
            if (pictureClaimValue.HasValue())
            {
                // only update the picture if this is the first time we've got a picture or if the picture is different to the one we've already downloaded
                if (!user.Picture.HasValue() || !user.PictureHostedUrl.HasValue() || !user.Picture.Equals(pictureClaimValue, StringComparison.CurrentCultureIgnoreCase))
                {
                    await Server.Instance.Users.DownloadAndStoreUserPictureAsync(user, pictureClaimValue);
                    updateNeeded = true;
                }
            }

            if (updateNeeded)
            {
                // we'll either create them or update them, which is useful if their
                // profile picture has changed from their source identity provider, i.e. Facebook
                await Server.Instance.Users.CreateOrUpdateUserAsync(user);
            }
        }
    }
}
