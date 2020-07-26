using System;

namespace LB.PhotoGalleries.Application.Models
{
    public class Category
    {
        #region accessors
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        #endregion

        #region constructors
        public Category()
        {
        }
        #endregion
    }
}