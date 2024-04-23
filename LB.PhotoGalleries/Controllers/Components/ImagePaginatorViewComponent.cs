using LB.PhotoGalleries.Models;
using Microsoft.AspNetCore.Mvc;

namespace LB.PhotoGalleries.Controllers.Components;

public class ImagePaginatorViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(PagedResultSet<Image> pagedResultSet)
    {
        return View(pagedResultSet);
    }
}