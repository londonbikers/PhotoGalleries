using Newtonsoft.Json;

namespace LB.PhotoGalleries.Application.Models
{
    public class Category
    {
        #region accessors
        [JsonProperty("id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>
        /// Used by CosmosDB to partition container contents to make querying more performant.
        /// The value should be an arbitrary constant as we don't have many categories so all items can reside in the same partition.
        /// </summary>
        public string PartitionKey { get; set; }
        #endregion

        #region constructors
        public Category()
        {
        }
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
                string.IsNullOrEmpty(Description) ||
                string.IsNullOrEmpty(PartitionKey))
                return false;

            return true;
        }
        #endregion
    }
}
