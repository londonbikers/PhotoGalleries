using Imageflow.Fluent;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using System.Collections.Generic;

namespace LB.PhotoGalleries.ComparisonTool
{
    internal static class ImageFileSpecsTesting15s
    {
        internal static List<ImageFileSpec> ProduceImageFileSpecs()
        {
            return new()
            {
                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 25, 15f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 50, 15f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 75, 15f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 80, 15f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 90, 15f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.WebPLossy, 400, 80, FileSpec.SpecLowRes.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.WebPLossy, 400, 80, 15f, InterpolationFilter.Mitchell, FileSpec.SpecLowRes.ToString()),

                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 50, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 70, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 80, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 100, 15f, InterpolationFilter.Mitchell, FileSpec.Spec800.ToString()),
                // super high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossy, 800, 90, 15f, InterpolationFilter.Mitchell,  FileSpec.Spec800.ToString()),


                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 50, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 70, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 80, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 100, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec1920.ToString()),
                
                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 50, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 70, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 80, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 100, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossy, 2560, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec2560.ToString()),

                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 50, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 70, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 80, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 100, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossy, 3840, 90, 15f, InterpolationFilter.Mitchell, FileSpec.Spec3840.ToString()),
            };
        }
    }
}
