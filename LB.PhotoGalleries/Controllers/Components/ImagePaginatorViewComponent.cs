using LB.PhotoGalleries.Application.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Controllers.Components
{
    public class ImagePaginatorViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(PagedResultSet<Image> pagedResultSet)
        {
            return View(pagedResultSet);
        }
    }
}
