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
            new ImageFileSpec(FileSpec.SpecOriginal, "originals"),
            new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString().ToLower()),
            new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 25, "lowres")
        };

        public static ImageFileSpec GetImageFileSpec(FileSpec fileSpec)
        {
            return Specs.Single(ifs => ifs.FileSpec == fileSpec);
        }
    }
}
