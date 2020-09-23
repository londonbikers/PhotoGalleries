using System;
using System.Collections.Generic;

namespace LB.PhotoGalleries.Application.Models
{
    public class PagedResultSet<T>
    {
        #region accessors
        public List<T> Results { get; set; }
        public int TotalResults { get; set; }
        public int PageSize { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages
        {
            get 
            {
                var pages = TotalResults / PageSize;
                var pagesDecimal = Convert.ToDecimal(pages);
                var roundedUpDecimal = Math.Ceiling(pagesDecimal);
                var roundedUpInt = Convert.ToInt32(roundedUpDecimal);

                // if we have some items but less than a page size then return one page, not zero
                if (TotalResults > 0 && roundedUpInt < 1)
                    roundedUpInt = 1;

                return roundedUpInt;
            }
        }
        #endregion

        #region constructors
        public PagedResultSet()
        {
            Results = new List<T>();
        }
        #endregion

        #region public methods
        public int[] GetNavigationPageNumbers(int pagesToShow)
        {
            var numbers = new int[pagesToShow];

            // if we're halfway or less from the middle of the range in relation to the overall page list then just list from page 1 and up
            var middleOfRangeDouble = (double)pagesToShow / (double)2;
            var middleOfRange = Math.Ceiling(middleOfRangeDouble);
            if (CurrentPage <= middleOfRange)
            {
                // we're near the start, anchor on the start
                for (var i = 0; i < pagesToShow; i++)
                    numbers[i] = i+1;

                return numbers;
            }

            // if we're halfway or less to the end of the range in relation to the overall page list then just list from the last page minus the range
            var pagesFromEnd = TotalPages - CurrentPage;
            if (pagesFromEnd <= middleOfRange)
            {
                // we're near the end of pages, anchor on the last page
                var page = TotalPages - pagesToShow + 1;
                for (var i = 0; i < pagesToShow; i++)
                {
                    numbers[i] = page;
                    page++;
                }

                return numbers;
            }

            // otherwise anchor the current page on the middle of the range of pages we want to show
            var positionDouble = (double) pagesToShow / (double) 2;
            var position = Math.Ceiling(positionDouble); // we have the anchor now
            var startNumber = Convert.ToInt32(position - positionDouble);

            for (var i = 0; i < pagesToShow; i++)
            {
                numbers[i] = startNumber;
                startNumber++;
            }

            return numbers;
        }
        #endregion
    }
}
