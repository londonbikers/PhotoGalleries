using LB.PhotoGalleries.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace LB.PhotoGalleries
{
    public class Utilities
    {
        /// <summary>
        /// Retrieves the unique identifier for a currently logged-in user.
        /// The id is provided by the Identity Provider from the sub claim as part of the OpenID Connect authentication.
        /// </summary>
        public static string GetUserId(ClaimsPrincipal user)
        {
            return user.FindFirstValue("sub");
        }

        /// <summary>
        /// Returns a new unique identifier for an object.
        /// </summary>
        public static string CreateNewId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Determines if a user is authorised to edit or delete a photos object.
        /// </summary>
        public static bool IsUserAuthorisedToEdit(ClaimsPrincipal user, string objectCreatedByUserId)
        {
            // users must be an administrator or have created the object to edit (and delete) a photos object
            if (user.IsInRole(Roles.Administrator.ToString()))
                return true;

            if (GetUserId(user) == objectCreatedByUserId)
                return true;

            return false;
        }

        /// <summary>
        /// Orders images by position if set, or when they were created if not.
        /// </summary>
        public static IOrderedEnumerable<Image> OrderImages(List<Image> images)
        {
            if (images.Any(i => i.Position.HasValue))
                return images.OrderBy(i => i.Position.Value);

            return images.OrderBy(i => i.Created);
        }
    }

    public enum Roles
    {
        Administrator,
        Photographer
    }
}
