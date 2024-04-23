using LB.PhotoGalleries.Models;
using Microsoft.AspNetCore.Mvc;

namespace LB.PhotoGalleries.Controllers.Components;

public class OpenGraphViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(OpenGraphModel openGraphModel)
    {
        return View(openGraphModel);
    }
}