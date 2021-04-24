﻿using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Shared;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Security.Claims;

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
        /// Determines if a user is authorised to edit or delete a photos object.
        /// </summary>
        public static bool CanUserEditObject(ClaimsPrincipal user, User objectCreatedByUser)
        {
            // users must be an administrator or have created the object to edit (and delete) a photos object
            if (user.IsInRole(Roles.Administrator.ToString()))
                return true;

            if (objectCreatedByUser != null)
                return CanUserEditObject(user, objectCreatedByUser.Id);

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
                .Replace("@", string.Empty)
                .Replace("'", string.Empty)
                .Replace("#", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
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
            if (string.IsNullOrEmpty(image.Metadata.CameraMake) || string.IsNullOrEmpty(image.Metadata.CameraModel))
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

        public static string GetFirstParagraph(string text)
        {
            var paragraphs = GetParagraphs(text);
            return paragraphs != null ? paragraphs[0] : text;
        }

        public static string GetSubsequentParagraphs(string text)
        {
            var paragraphs = GetParagraphs(text);
            if (paragraphs == null)
                return null;

            if (paragraphs.Length == 1)
                return null;

            var subsequentParagraphs = string.Empty;
            for (var i = 1; i < paragraphs.Length; i++)
                subsequentParagraphs += paragraphs[i] + "\r\n\r\n";

            if (subsequentParagraphs.EndsWith("\r\n\r\n"))
                subsequentParagraphs = subsequentParagraphs.Substring(0, subsequentParagraphs.Length - 4);

            return subsequentParagraphs;

        }

        /// <summary>
        /// Returns a URL for the current query that changes what search results are displayed.
        /// </summary>
        public static string GetSearchTypeUrl(SearchResultsType searchResultsType, string query)
        {
            if (searchResultsType == SearchResultsType.All)
                return $"/search?q={query}";
            else
                return $"/search?q={query}&t={searchResultsType.ToString().ToLower()}";
        }

        /// <summary>
        /// Returns the absolute URL of an image.
        /// </summary>
        public static string GetFullImageUrl(IConfiguration config, Image image)
        {
            var baseUrl = config["BaseUrl"];
            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            return $"{baseUrl}gi/{image.GalleryId}/{image.Id}/{EncodeParamForUrl(image.Name)}";
        }

        /// <summary>
        /// Returns the absolute URL of an image that links to a specific user comment.
        /// </summary>
        public static string GetFullImageUrl(IConfiguration config, Image image, DateTime commentCreated)
        {
            var imageUrl = GetFullImageUrl(config, image);
            return $"{imageUrl}?c={commentCreated.Ticks}";
        }

        /// <summary>
        /// Returns the absolute URL of a gallery.
        /// </summary>
        public static string GetFullGalleryUrl(IConfiguration config, Gallery gallery)
        {
            var baseUrl = config["BaseUrl"];
            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            return $"{baseUrl}g/{gallery.CategoryId}/{gallery.Id}/{EncodeParamForUrl(gallery.Name)}";
        }

        /// <summary>
        /// Returns the absolute URL of a gallery that links to a specific user comment.
        /// </summary>
        public static string GetFullGalleryUrl(IConfiguration config, Gallery gallery, DateTime commentCreated)
        {
            var imageUrl = GetFullGalleryUrl(config, gallery);
            return $"{imageUrl}?c={commentCreated.Ticks}";
        }

        #region private methods
        /// <summary>
        /// Breaks text up into paragraphs.
        /// </summary>
        private static string[] GetParagraphs(string text)
        {
            if (text.Contains("\r\n\r\n"))
                return text.Split("\r\n\r\n", StringSplitOptions.RemoveEmptyEntries);

            if (text.Contains("\n\n\n\n"))
                return text.Split("\n\n\n\n", StringSplitOptions.RemoveEmptyEntries);

            return null;
        }
        #endregion
    }

    public enum Roles
    {
        Administrator,
        Photographer
    }
}
