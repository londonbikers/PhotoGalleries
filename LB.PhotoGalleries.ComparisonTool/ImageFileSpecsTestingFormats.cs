using Imageflow.Fluent;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using System.Collections.Generic;

namespace LB.PhotoGalleries.ComparisonTool
{
    internal static class ImageFileSpecsTestingFormats
    {
        internal static List<ImageFileSpec> ProduceImageFileSpecs()
        {
            return new()
            {
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 25, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.WebP, 400, 25, FileSpec.SpecLowRes.ToString()),

                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 100, 15f, InterpolationFilter.Robidoux, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebP, 800, 90, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebP, 800, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),

                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebP, 1920, 90, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebP, 1920, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),

                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebP, 2560, 90, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebP, 2560, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebP, 3840, 90, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebP, 3840, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString())
            };
        }
    }
}
