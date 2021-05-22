using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using System.Collections.Generic;

namespace LB.PhotoGalleries.ComparisonTool
{
    internal static class ImageFileSpecsTestingSharpness
    {
        internal static List<ImageFileSpec> ProduceImageFileSpecs()
        {
            return new()
            {
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 80, 0f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 80, 10f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 80, 20f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 80, 25f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 80, 50f, FileSpec.SpecLowRes.ToString()),

                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 80, 0f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 80, 10f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 80, 20f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 80, 25f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 80, 50f, FileSpec.Spec800.ToString()),


                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 80, 0f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 80, 10f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 80, 20f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 80, 25f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 80, 50f, FileSpec.Spec1920.ToString()),


                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 80, 0f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 80, 10f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 80, 20f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 80, 25f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 80, 50f, FileSpec.Spec2560.ToString()),

                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 80, 0f, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 80, 10f, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 80, 20f, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 80, 25f, FileSpec.Spec3840.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 80, 50f, FileSpec.Spec3840.ToString()),
            };
        }
    }
}
