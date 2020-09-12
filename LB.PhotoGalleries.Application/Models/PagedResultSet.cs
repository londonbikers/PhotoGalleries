using System.Collections.Generic;

namespace LB.PhotoGalleries.Application.Models
{
    public class PagedResultSet<T>
    {
        public List<T> Results { get; set; }
        public int TotalResults { get; set; }
        public int PageSize { get; set; }

        public int CurrentPage { get; set; }

        public PagedResultSet()
        {
            Results = new List<T>();
        }
    }
}
