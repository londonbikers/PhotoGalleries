using System;
namespace LB.PhotoGalleries.Models
{
    public class Comment
    {
        #region accessors
        public DateTime Created { get; set; }
        public string Text { get; set; }
        #endregion

        #region constructors
        public Comment()
        {
        }
        #endregion
    }
}