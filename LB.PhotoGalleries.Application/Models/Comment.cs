using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LB.PhotoGalleries.Application.Models
{
    public class Comment
    {
        #region accessors
        public DateTime Created { get; set; }
        [DisplayName("Created by user id")]
        public string CreatedByUserId { get; set; }
        [Required]
        public string Text { get; set; }
        #endregion

        #region constructors
        public Comment()
        {
            Created = DateTime.Now;
        }
        #endregion
    }
}
