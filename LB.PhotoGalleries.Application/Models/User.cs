using System.Text.Json;

namespace LB.PhotoGalleries.Application.Models
{
    public class User
    {
        #region accessors
        public string Id { get; set; }
        public string Name { get; set; }
        public string Picture { get; set; }
        public string Email { get; set; }
        #endregion

        #region public methods
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
        #endregion
    }
}