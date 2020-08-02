using Newtonsoft.Json;

namespace LB.PhotoGalleries.Application.Models
{
    public class User
    {
        #region members
        private string _name;
        #endregion

        #region accessors
        [JsonProperty("id")]
        public string Id { get; set; }
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                // also set the partition key as the first character of the name
                _name = value;
                PartitionKey = _name.Substring(0, 1).ToLower();
            }
        }
        public string Picture { get; set; }
        public string Email { get; set; }
        /// <summary>
        /// Used by CosmosDB to partition container items to improve querying performance.
        /// The value should be the first letter of the Name property.
        /// </summary>
        public string PartitionKey { get; set; }
        #endregion

        #region public methods
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public bool IsValid()
        {
            if (string.IsNullOrEmpty(Id) ||
                string.IsNullOrEmpty(Name) ||
                string.IsNullOrEmpty(Email) ||
                string.IsNullOrEmpty(PartitionKey))
                return false;

            return true;
        }
        #endregion
    }
}
