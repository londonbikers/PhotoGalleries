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
            new ImageFileSpec(FileSpec.SpecOriginal, 0, 0, "originals"),
            new ImageFileSpec(FileSpec.Spec3840, 3840, 80, FileSpec.Spec3840.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec2560, 2560, 80, FileSpec.Spec2560.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec1920, 1920, 80, FileSpec.Spec1920.ToString().ToLower()),
            new ImageFileSpec(FileSpec.Spec800, 800, 80, FileSpec.Spec800.ToString().ToLower()),
            new ImageFileSpec(FileSpec.SpecLowRes, 400, 75, "lowres")
        };

        public static ImageFileSpec GetImageFileSpec(FileSpec fileSpec)
        {
            return Specs.Single(ifs => ifs.FileSpec == fileSpec);
        }
    }
}
