namespace LB.PhotoGalleries.Models.Enums;

/// <summary>
/// Names of file specs we use to pre-generate images for.
/// </summary>
public enum FileSpec
{
    SpecOriginal,
    Spec3840,
    Spec2560,
    Spec1920,
    Spec800,
    SpecLowRes
}

/// <summary>
/// Describes a supported file format.
/// </summary>
public enum FileSpecFormat
{
    Undefined,
    Jpeg,
    WebPLossless,
    WebPLossy
}