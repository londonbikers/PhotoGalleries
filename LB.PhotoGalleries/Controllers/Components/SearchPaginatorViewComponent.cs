using LB.PhotoGalleries.Models;
using Microsoft.AspNetCore.Mvc;

namespace LB.PhotoGalleries.Controllers.Components;

public class SearchPaginatorViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(SearchPagedResultSet searchPagedResultSet)
    {
        return View(searchPagedResultSet);
    }
}