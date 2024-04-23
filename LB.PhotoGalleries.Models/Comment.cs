using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LB.PhotoGalleries.Models;

public class Comment
{
    #region accessors
    public DateTime Created { get; set; } = DateTime.UtcNow;

    [DisplayName("Created by user id")]
    public string CreatedByUserId { get; set; }
    [Required]
    public string Text { get; set; }
    #endregion
}
