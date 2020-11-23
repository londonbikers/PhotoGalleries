using System;
using System.Collections.Generic;

namespace LB.PhotoGalleries.Models
{
    public class SearchPagedResultSet : PagedResultSet<object>
    {
        public List<Category> CategoryResults { get; set; }
        public int TotalCategoryResults { get; set; }

        public List<Gallery> GalleryResults { get; set; }
        public int TotalGalleryResults { get; set; }

        public List<Image> ImageResults { get; set; }
        public int TotalImageResults { get; set; }

        public override int TotalPages
        {
            get
            {
                // take the biggest total results
                var biggestTotalResults = Math.Max(Math.Max(TotalCategoryResults, TotalGalleryResults), TotalImageResults);
                
                var pages = (double)biggestTotalResults / (double)PageSize;
                var roundedUpDecimal = Math.Ceiling(pages);
                var roundedUpInt = Convert.ToInt32(roundedUpDecimal);

                // if we have some items but less than a page size then return one page, not zero
                if (biggestTotalResults > 0 && roundedUpInt < 1)
                    roundedUpInt = 1;

                return roundedUpInt;
            }
        }

        [Obsolete("Not supported for SearchPagedResultSet. Use individual total result properties instead, i.e. ImageTotalResults.")]
        #pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override int TotalResults { get; set; }
        #pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

        /// <summary>
        /// The URL query string used to perform the search, i.e 'q=honda&t=g'.
        /// Don't include '?'
        /// Don't include paging information
        /// </summary>
        public string QueryString { get; set; }
    }
}
