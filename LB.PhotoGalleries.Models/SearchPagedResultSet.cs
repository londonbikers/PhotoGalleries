using LB.PhotoGalleries.Models.Enums;
using System;
using System.Collections.Generic;

namespace LB.PhotoGalleries.Models;

public class SearchPagedResultSet : PagedResultSet<object>
{
    public SearchResultsType SearchResultsType { get; set; }

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
    /// The user-supplied term to search for, i.e. 'honda'.
    /// </summary>
    public string SearchTerm { get; set; }

    #region search methods
    public string BuildSearchQueryString(QueryRange queryRange)
    {
        var query = $"/search?q={SearchTerm}";

        if (SearchResultsType != SearchResultsType.All)
            query += $"&t={SearchResultsType.ToString().ToLower()}";

        if (CurrentPage > 1)
            query += "&p=" + CurrentPage;

        if (QuerySortBy != QuerySortBy.DateCreated)
            query += "&s=" + QuerySortBy.ToString().ToLower();

        query += "&r=" + queryRange.ToString().ToLower();
        return query;
    }

    public string BuildSearchQueryString(QuerySortBy querySortBy)
    {
        var query = $"/search?q={SearchTerm}";

        if (SearchResultsType != SearchResultsType.All)
            query += $"&t={SearchResultsType.ToString().ToLower()}";

        if (CurrentPage > 1)
            query += "&p=" + CurrentPage;

        query += "&s=" + querySortBy.ToString().ToLower();

        if (QueryRange != QueryRange.Forever)
            query += "&r=" + QueryRange.ToString().ToLower();

        return query;
    }

    public string BuildSearchQueryString(SearchResultsType searchResultsType)
    {
        var query = $"/search?q={SearchTerm}";
        query += $"&t={searchResultsType.ToString().ToLower()}";

        if (CurrentPage > 1)
            query += "&p=" + CurrentPage;

        if (QuerySortBy != QuerySortBy.DateCreated)
            query += "&s=" + QuerySortBy.ToString().ToLower();

        if (QueryRange != QueryRange.Forever)
            query += "&r=" + QueryRange.ToString().ToLower();

        return query;
    }

    public string BuildSearchQueryString(int pageNumber)
    {
        var query = $"/search?q={SearchTerm}";
        var paramz = new List<string>();

        if (SearchResultsType != SearchResultsType.All)
            paramz.Add("t=" + SearchResultsType.ToString().ToLower());

        if (pageNumber > 1)
            paramz.Add("p=" + pageNumber);

        if (QuerySortBy != QuerySortBy.DateCreated)
            paramz.Add("s=" + QuerySortBy.ToString().ToLower());

        if (QueryRange != QueryRange.Forever)
            paramz.Add("r=" + QueryRange.ToString().ToLower());

        if (paramz.Count == 0)
            return query;

        query += "&" + string.Join('&', paramz);
        return query;
    }
    #endregion
}