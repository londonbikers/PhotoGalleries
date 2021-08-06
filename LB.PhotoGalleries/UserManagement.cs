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
        public static async Task UpdateUserFromClaimsAsync(TicketReceivedContext ctx)
        {
            var userId = ctx.Principal.FindFirstValue("sub");
            var user = await Server.Instance.Users.GetUserAsync(userId);
            var updateNeeded = false;

            if (user == null)
            {
                // the user is new, create them
                user = new User
                {
                    Id = ctx.Principal.FindFirstValue("sub"),
                    Name = ctx.Principal.FindFirstValue("name"),
                    Email = ctx.Principal.FindFirstValue("email"),
                    Picture = ctx.Principal.FindFirstValue("picture"),
                    LegacyApolloId = ctx.Principal.FindFirstValue("urn:londonbikers:legacyapolloid")
                };

                // set any defaults
                user.CommunicationPreferences.ReceiveCommentNotifications = true;

                updateNeeded = true;
            }
            else
            {
                // we already have an existing user for them, update their attributes if necessary
                if (!user.Name.Equals(ctx.Principal.FindFirstValue("name"), StringComparison.CurrentCultureIgnoreCase))
                {
                    user.Name = ctx.Principal.FindFirstValue("name");
                    updateNeeded = true;
                }

                if (!user.Email.Equals(ctx.Principal.FindFirstValue("email"), StringComparison.CurrentCultureIgnoreCase))
                {
                    user.Email = ctx.Principal.FindFirstValue("email");
                    updateNeeded = true;
                }

                var pictureClaimValue = ctx.Principal.FindFirstValue("picture");
                if (pictureClaimValue.HasValue())
                {
                    // only update the picture if we have an inbound claim
                    if (!user.Picture.HasValue() || !user.Picture.Equals(pictureClaimValue, StringComparison.CurrentCultureIgnoreCase))
                    {
                        // only update the picture if this is the first time we've got a picture or if the picture is different to the one we've already downloaded
                        await Server.Instance.Users.DownloadAndStoreUserPictureAsync(user, pictureClaimValue);
                        updateNeeded = true;
                    }
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
