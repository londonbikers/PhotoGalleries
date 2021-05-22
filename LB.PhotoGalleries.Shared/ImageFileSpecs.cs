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
            new ImageFileSpec(FileSpec.SpecOriginal, FileSpecFormat.Undefined, 0, 0, 0f, "originals"),
            new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 80, 25f,FileSpec.Spec3840.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 80, 25f, FileSpec.Spec2560.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 80, 25f, FileSpec.Spec1920.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 80, 25f, FileSpec.Spec800.ToString().ToLower()),
            new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 75, 0f, "lowres")
        };

        public static ImageFileSpec GetImageFileSpec(FileSpec fileSpec)
        {
            return Specs.Single(ifs => ifs.FileSpec == fileSpec);
        }
    }
}
