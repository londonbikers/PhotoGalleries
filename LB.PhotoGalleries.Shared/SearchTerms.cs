using System;
using System.Collections.Generic;
using System.Linq;

namespace LB.PhotoGalleries.Shared
{
    /// <summary>
    /// Expands a search query into the variants we look for, so a search for "cars" finds "car", and "kwak" finds "kawasaki".
    /// </summary>
    public static class SearchTerms
    {
        /// <summary>
        /// Words that mean the same thing. A search for any word in a group also searches for the rest of the group.
        /// Searches match substrings, so avoid short words that turn up inside other words, e.g. "lid" is in "slide".
        /// </summary>
        private static readonly string[][] SynonymGroups =
        [
            ["bike", "motorbike", "motorcycle"],
            ["kawasaki", "kwak", "kawi"],
            ["triumph", "trumpet"],
            ["ktm", "katoom"],
            ["harley", "hog"],
            ["gixxer", "gsxr", "gsx-r"],
            ["hayabusa", "busa"],
            ["fireblade", "blade"],
            ["blackbird", "cbr1100xx"],
            ["bonneville", "bonnie"],
            ["bmw", "beemer", "beamer"],
            ["sportsbike", "sports bike", "sportbike", "crotch rocket"],
            ["supermoto", "supermotard"],
            ["cafe racer", "café racer"],
            ["helmet", "skid lid", "brain bucket"],
            ["trackday", "track day"]
        ];

        /// <summary>
        /// Words ending in "s" that aren't plurals, or whose singular isn't made by removing the "s".
        /// </summary>
        private static readonly HashSet<string> NotPlurals = new(StringComparer.OrdinalIgnoreCase)
        {
            "series", "species", "news", "chassis", "always", "lens"
        };

        /// <summary>
        /// Each variant adds a clause to the database query, which costs more, so don't let it grow without limit.
        /// </summary>
        public const int MaxVariants = 8;

        /// <summary>
        /// Returns the lower-case variants of a query to search for. Searches match substrings, so the singular form also
        /// finds the plural and the query itself doesn't need to be included when it's a plural.
        /// </summary>
        public static List<string> Expand(string query)
        {
            if (!query.HasValue())
                return [];

            var words = query.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var phrase = string.Join(' ', words);
            var variants = new List<string>();

            // a synonym of the whole query, e.g. "track day"
            var group = FindGroup(phrase);
            if (group != null)
            {
                variants.Add(Singularise(phrase));
                variants.AddRange(group);
                return Distinct(variants);
            }

            // otherwise expand the last word, so "red bikes" searches for "red bike", "red motorbike" and "red motorcycle"
            var prefix = words.Length > 1 ? string.Join(' ', words[..^1]) + " " : string.Empty;
            var last = Singularise(words[^1]);
            variants.Add(prefix + last);

            group = FindGroup(last);
            if (group != null)
                variants.AddRange(group.Select(w => prefix + w));

            return Distinct(variants);
        }

        /// <summary>
        /// Turns an English plural into its singular, e.g. "bikes" into "bike", "boxes" into "box" and "bodies" into "body".
        /// Leaves anything else alone.
        /// </summary>
        public static string Singularise(string word)
        {
            if (word.Length < 4 || NotPlurals.Contains(word))
                return word;

            if (word.EndsWith("ies"))
                return word[..^3] + "y";

            if (word.EndsWith("sses") || word.EndsWith("shes") || word.EndsWith("ches") || word.EndsWith("xes") || word.EndsWith("zes"))
                return word[..^2];

            if (word.EndsWith('s') && !word.EndsWith("ss") && !word.EndsWith("us") && !word.EndsWith("is"))
                return word[..^1];

            return word;
        }

        private static string[] FindGroup(string word)
        {
            return SynonymGroups.FirstOrDefault(g => g.Contains(word) || g.Contains(Singularise(word)));
        }

        private static List<string> Distinct(List<string> variants)
        {
            return variants.Distinct().Take(MaxVariants).ToList();
        }
    }
}
