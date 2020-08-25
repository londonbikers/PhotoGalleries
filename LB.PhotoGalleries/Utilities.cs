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

        /// <summary>
        /// Encodes text we want to use as a URL parameter. Provides a simpler and more aesthetically pleasing encode than traditional Url Encode functions.
        /// </summary>
        public static string EncodeParamForUrl(string parameter)
        {
            return parameter.Replace("-", "_").Replace(" ", "-").ToLower();
        }

        /// <summary>
        /// Turns encoded parameters back into usable text. Will not undo lower-casing.
        /// </summary>
        public static string DecodeParameterFromUrl(string parameter)
        {
            return parameter.Replace("-", " ").Replace("_", "-");
        }
    }

    public enum Roles
    {
        Administrator,
        Photographer
    }
}
