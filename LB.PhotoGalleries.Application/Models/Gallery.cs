using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LB.PhotoGalleries.Application.Models
{
    public class Gallery
    {
        #region accessors
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Active { get; set; }
        public Dictionary<int, Image> Images { get;set;}
        public List<Comment> Comments { get; set; }
        public string CreatedByUserId { get; set; }
        public DateTime Created { get; set; }
        #endregion

        #region constructors
        public Gallery()
        {
            Images = new Dictionary<int, Image>();
            Comments = new List<Comment>();
            Created = DateTime.Now;
        }
        #endregion

        #region public methods
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
        #endregion
    }
}