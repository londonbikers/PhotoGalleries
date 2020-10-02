using System;
using System.IO;

namespace LB.PhotoGalleries.Shared
{
    public class Utilities
    {
        /// <summary>
        /// Does what it says on the tin. Converts a stream to a byte array.
        /// </summary>
        public static byte[] ConvertStreamToBytes(Stream input)
        {
            if (input == null)
                return null;

            if (input.CanSeek && input.Position != 0)
                input.Position = 0;

            using var ms = new MemoryStream();
            input.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Generates a new unique identifier for use on objects.
        /// </summary>
        public static string GenerateId()
        {
            return Guid.NewGuid().ToString().Replace("-", string.Empty);
        }

        /// <summary>
        /// Base64 encodes a string.
        /// </summary>
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedText)
        {
            var data = Convert.FromBase64String(base64EncodedText);
            var decoded = System.Text.Encoding.ASCII.GetString(data);
            return decoded;
        }
    }
}
