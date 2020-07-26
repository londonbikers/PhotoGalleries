using System;
using System.Collections.Generic;

namespace LB.PhotoGalleries.Application.Models
{
    public class Image
    {
        #region accessors
        public string Name { get; set; }
        public string Comment { get; set; }
        public string Credit { get; set; }
        public DateTime CaptureDate { get; set; }
        public List<Comment> Comments { get; set; }
        #endregion
        
        #region constructors
        public Image()
        {
            Comments = new List<Comment>();
        }
        #endregion
    }
}