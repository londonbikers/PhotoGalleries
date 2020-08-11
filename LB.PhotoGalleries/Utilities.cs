using System;
using System.Security.Claims;

namespace LB.PhotoGalleries
{
    public class Utilities
    {
        /// <summary>
        /// Retrieves the unique identifier for the currently logged-in user.
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
    }
}
