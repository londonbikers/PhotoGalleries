using LB.PhotoGalleries.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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

        /// <summary>
        /// Orders images by position if set, or when they were created if not.
        /// </summary>
        public static IOrderedEnumerable<Image> OrderImages(List<Image> images)
        {
            if (images.All(i => i.Position.HasValue))
                // ReSharper disable once PossibleInvalidOperationException - already checked in query
                return images.OrderBy(i => i.Position.Value);

            return images.OrderBy(i => i.Created);
        }

        public static string ListToCsv(List<string> list)
        {
            return string.Join(',', list);
        }

        public static List<string> CsvToList(string csv)
        {
            return string.IsNullOrEmpty(csv) ? null : csv.Split(',').ToList();
        }

        /// <summary>
        /// Adds a new tag (won't add a duplicate) to the tags CSV.
        /// </summary>
        public static string AddTagToCsv(string tags, string tag)
        {
            // if this is the first tag just return it!
            var list = CsvToList(tags);
            if (list == null)
                return tag;

            // make sure we're not adding duplicates
            if (!list.Any(t => t.Trim().Equals(tag.Trim(), StringComparison.CurrentCultureIgnoreCase)))
                list.Add(tag.Trim().ToLower());

            return ListToCsv(list);
        }

        /// <summary>
        /// Removes an instance of a tag from the tags CSV.
        /// </summary>
        public static string RemoveTagFromCsv(string tags, string tag)
        {
            var list = CsvToList(tags);
            if (list == null || list.Count == 0)
                return null;

            list.RemoveAll(t => t.Trim().Equals(tag.Trim(), StringComparison.CurrentCultureIgnoreCase));
            return ListToCsv(list);
        }

        public static string TidyImageName(string name)
        {
            name = name.Replace("_", " ");
            name = Regex.Replace(name, " {2,}", " ", RegexOptions.Compiled);
            return name;
        }

        /// <summary>
        /// Validates that a file's magic number (file signature) matches an expected image format.
        /// This prevents malicious files being uploaded disguised as images by checking actual file content,
        /// not just the HTTP Content-Type header which can be spoofed.
        /// </summary>
        /// <param name="stream">The file stream to validate. Stream position will be reset to 0 after validation.</param>
        /// <returns>True if the file signature matches a valid image format (JPEG, PNG), false otherwise.</returns>
        public static bool ValidateImageFileSignature(Stream stream)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek)
                return false;

            // Store original position to reset after validation
            var originalPosition = stream.Position;

            try
            {
                // Read first 12 bytes to check file signatures (some formats need more bytes)
                stream.Position = 0;
                var headerBytes = new byte[12];
                var bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);

                if (bytesRead < 2)
                    return false;

                // JPEG: FF D8 FF
                if (headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF)
                    return true;

                // PNG: 89 50 4E 47 0D 0A 1A 0A
                if (bytesRead >= 8 &&
                    headerBytes[0] == 0x89 &&
                    headerBytes[1] == 0x50 &&
                    headerBytes[2] == 0x4E &&
                    headerBytes[3] == 0x47 &&
                    headerBytes[4] == 0x0D &&
                    headerBytes[5] == 0x0A &&
                    headerBytes[6] == 0x1A &&
                    headerBytes[7] == 0x0A)
                    return true;

                // File signature doesn't match any supported image format
                return false;
            }
            finally
            {
                // Always reset stream position
                stream.Position = originalPosition;
            }
        }
    }
}
