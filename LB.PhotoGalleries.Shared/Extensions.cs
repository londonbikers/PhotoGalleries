using System.Linq;

namespace LB.PhotoGalleries.Shared
{
    public static class Extensions
    {
        /// <summary>
        /// Determines if a string has a usable value, i.e. is not null, empty or made up of just whitespace.
        /// </summary>
        public static bool HasValue(this string str)
        {
            return !string.IsNullOrEmpty(str) && !string.IsNullOrWhiteSpace(str);
        }

        /// <summary>
        /// Determines if a TagsCsv contains a specific tag.
        /// </summary>
        public static bool TagsContain(this string str, string tag)
        {
            if (!str.HasValue())
                return false;

            var list = str.ToLower().Split(',').ToList();
            for (var x = 0; x < list.Count; x++)
                list[x] = list[x].Trim();

            var tagPresent = list.Contains(tag.ToLower().Trim());
            return tagPresent;
        }
    }
}
