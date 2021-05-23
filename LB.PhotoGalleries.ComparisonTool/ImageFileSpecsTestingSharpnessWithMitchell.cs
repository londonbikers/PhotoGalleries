using Imageflow.Fluent;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using System.Collections.Generic;

namespace LB.PhotoGalleries.ComparisonTool
{
    internal static class ImageFileSpecsTestingSharpnessWithMitchell
    {
        internal static List<ImageFileSpec> ProduceImageFileSpecs()
        {
            return new()
            {
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 90, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 90, 10f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 90, 15f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 90, 20f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 90, 25f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 90, 50f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),

                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 10f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 20f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 25f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 50f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),

                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 10f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 20f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 25f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 50f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),

                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 10f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 20f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 25f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 50f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),

                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 10f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 20f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 25f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 50f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
            };
        }
    }
}
