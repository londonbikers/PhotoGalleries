using LB.PhotoGalleries.Models;
using System;
using System.Linq;
using System.Security.Claims;
using LB.PhotoGalleries.Shared;

namespace LB.PhotoGalleries
{
    public class Helpers
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
            return Guid.NewGuid().ToString().Replace("-", string.Empty);
        }

        /// <summary>
        /// Determines if a user is authorised to edit or delete a photos object.
        /// </summary>
        public static bool CanUserEditObject(ClaimsPrincipal user, string objectCreatedByUserId)
        {
            // users must be an administrator or have created the object to edit (and delete) a photos object
            if (user.IsInRole(Roles.Administrator.ToString()))
                return true;

            if (objectCreatedByUserId.HasValue() && GetUserId(user) == objectCreatedByUserId)
                return true;

            return false;
        }

        /// <summary>
        /// Determines if a user can edit/delete a comment against an image or a gallery.
        /// </summary>
        /// <param name="comment">The comment to be edited/deleted.</param>
        /// <param name="gallery">The gallery the comment is made against, or the gallery the image resides in if the comment is against an image.</param>
        /// <param name="user">The user being tested for authorisation.</param>
        /// <returns>True if they can edit/delete the comment. Otherwise false.</returns>
        public static bool CanUserEditComment(Comment comment, Gallery gallery, ClaimsPrincipal user)
        {
            var userId = GetUserId(user);
            return user.IsInRole(Roles.Administrator.ToString()) || gallery.CreatedByUserId == userId || comment.CreatedByUserId == userId;
        }

        /// <summary>
        /// Encodes text we want to use as a URL parameter. Provides a simpler and more aesthetically pleasing encode than traditional Url Encode functions.
        /// </summary>
        public static string EncodeParamForUrl(string parameter)
        {
            return parameter
                .Replace("-", "_")
                .Replace(" ", "-")
                .Replace("/","-")
                .ToLower();
        }

        /// <summary>
        /// Attempts to turn encoded parameters back into usable text. Will not undo lower-casing.
        /// </summary>
        public static string DecodeParameterFromUrl(string parameter)
        {
            return parameter.Replace("-", " ").Replace("_", "-");
        }

        /// <summary>
        /// Returns the name and model of the camera used to take a photo, if metadata allows.
        /// Attempts to de-duplicate manufacturer name from model name.
        /// </summary>
        public static string GetCameraName(Image image)
        {
            if (string.IsNullOrEmpty(image.Metadata.CameraMake) && string.IsNullOrEmpty(image.Metadata.CameraModel))
                return null;

            if (string.IsNullOrEmpty(image.Metadata.CameraModel) && !string.IsNullOrEmpty(image.Metadata.CameraMake))
                return image.Metadata.CameraMake;

            if (string.IsNullOrEmpty(image.Metadata.CameraMake) && !string.IsNullOrEmpty(image.Metadata.CameraModel))
                return image.Metadata.CameraModel;

            // got make and model info, try and de-dupe any mention of manufacturer from model
            // if the make is in the model, don't return the make
            if (image.Metadata.CameraModel.Contains(image.Metadata.CameraMake, StringComparison.CurrentCultureIgnoreCase))
                return image.Metadata.CameraModel;

            // sometimes the manufacturer is a long-form version of the one in the model so try and fish those out...
            var manufacturerWords = image.Metadata.CameraMake.Split(' ');
            if (manufacturerWords.Any(word => image.Metadata.CameraModel.Contains(word, StringComparison.CurrentCultureIgnoreCase)))
                return image.Metadata.CameraModel;

            // camera make doesn't seem to duplicate model so return both
            return image.Metadata.CameraMake + " " + image.Metadata.CameraModel;
        }
    }

    public enum Roles
    {
        Administrator,
        Photographer
    }
}
