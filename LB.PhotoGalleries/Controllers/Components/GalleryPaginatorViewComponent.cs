using LB.PhotoGalleries.Models;
using Microsoft.AspNetCore.Mvc;

namespace LB.PhotoGalleries.Controllers.Components
{
    public class GalleryPaginatorViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(PagedResultSet<Gallery> pagedResultSet)
        {
            return View(pagedResultSet);
        }
    }
}
