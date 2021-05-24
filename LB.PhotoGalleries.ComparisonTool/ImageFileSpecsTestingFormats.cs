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
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.WebPLossy, 400, 25, FileSpec.SpecLowRes.ToString()),
                //new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.WebPLossless, 400, FileSpec.SpecLowRes.ToString()),

                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 15f, InterpolationFilter.Robidoux, FileSpec.Spec800.ToString()),
                //new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossy, 800, 80, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossy, 800, 80, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossy, 800, 85, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                //new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossy, 800, 90, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossy, 800, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                //new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossless, 800, FileSpec.Spec800.ToString()),

                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                //new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 80, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 80, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                //new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 85, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 85, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                //new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 90, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                //new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 90, 25f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
//                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 90, 50f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                //new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossless, 1920, FileSpec.Spec1920.ToString()),

                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                //new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossy, 2560, 80,  FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossy, 2560, 80, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossy, 2560, 85, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                //new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossy, 2560, 90, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossy, 2560, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                //new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossless, 2560, FileSpec.Spec2560.ToString()),

                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                //new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossy, 3840, 80, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossy, 3840, 80, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossy, 3840, 85, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                //new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossy, 3840, 90, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossy, 3840, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                //new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossless, 3840, FileSpec.Spec3840.ToString())
            };
        }
    }
}
