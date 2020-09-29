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
    }
}
