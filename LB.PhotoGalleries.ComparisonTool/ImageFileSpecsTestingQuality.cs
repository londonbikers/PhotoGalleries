using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using System.Collections.Generic;

namespace LB.PhotoGalleries.ComparisonTool
{
    internal static class ImageFileSpecsTestingQuality
    {
        internal static List<ImageFileSpec> ProduceImageFileSpecs()
        {
            return new()
            {
                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 25, FileSpec.SpecLowRes.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 50, FileSpec.SpecLowRes.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 75, FileSpec.SpecLowRes.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 80, FileSpec.SpecLowRes.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 90, FileSpec.SpecLowRes.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.WebPLossy, 400, 50, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.WebPLossy, 400, 80, FileSpec.SpecLowRes.ToString()),

                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 50, FileSpec.Spec800.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 70, FileSpec.Spec800.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 80, FileSpec.Spec800.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, FileSpec.Spec800.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 100, FileSpec.Spec800.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossy, 800, 80, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebPLossy, 800, 90, FileSpec.Spec800.ToString()),

                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 50, FileSpec.Spec1920.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 70, FileSpec.Spec1920.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 80, FileSpec.Spec1920.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, FileSpec.Spec1920.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 100, FileSpec.Spec1920.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 80, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebPLossy, 1920, 90, FileSpec.Spec1920.ToString()),

                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 50, FileSpec.Spec2560.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 70, FileSpec.Spec2560.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 80, FileSpec.Spec2560.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, FileSpec.Spec2560.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 100, FileSpec.Spec2560.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossy, 2560, 80, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebPLossy, 2560, 90, FileSpec.Spec2560.ToString()),

                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 50, FileSpec.Spec3840.ToString()),
                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 70, FileSpec.Spec3840.ToString()),
                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 80, FileSpec.Spec3840.ToString()),
                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, FileSpec.Spec3840.ToString()),
                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 100, FileSpec.Spec3840.ToString()),
                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossy, 3840, 80, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebPLossy, 3840, 90, FileSpec.Spec3840.ToString())
            };
        }
    }
}
