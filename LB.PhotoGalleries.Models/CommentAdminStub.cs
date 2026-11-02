using System;

namespace LB.PhotoGalleries.Models;

/// <summary>
/// A comment along with what it was made on. Used in the admin area to list comments from galleries and images together.
/// </summary>
public class CommentAdminStub
{
    #region accessors
    public DateTime Created { get; set; }
    public string CreatedByUserId { get; set; }
    public string Text { get; set; }
    public string GalleryId { get; set; }
    public string GalleryCategoryId { get; set; }
    public string GalleryName { get; set; }
    /// <summary>
    /// Only set when the comment was made on an image rather than on the gallery itself.
    /// </summary>
    public string ImageId { get; set; }
    public string ImageName { get; set; }
    public bool IsImageComment => ImageId != null;
    #endregion
}
