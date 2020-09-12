using LB.PhotoGalleries.Application.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers.Components
{
    public class GalleryPaginatorViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(PagedResultSet<Gallery> pagedResultSet)
        {
            return View(pagedResultSet);
        }
    }
}
