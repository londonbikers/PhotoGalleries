using Imageflow.Fluent;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using System.Collections.Generic;
using System.Linq;

namespace LB.PhotoGalleries.Shared
{
    public static class ImageFileSpecs
    {
        public static List<ImageFileSpec> Specs => new List<ImageFileSpec>
        {
            new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossy, 3840, 80, 25f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossy, 2560, 80, 25f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 80, 25f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossy, 800, 80, 25f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString().ToLower()),
            new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.WebPLossy, 400, 25, "lowres")
        };

        public static ImageFileSpec GetImageFileSpec(FileSpec fileSpec)
        {
            return Specs.Single(ifs => ifs.FileSpec == fileSpec);
        }
    }
}
