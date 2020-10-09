using Newtonsoft.Json;
using System;

namespace LB.PhotoGalleries.Models
{
    public class User
    {
        #region accessors
        [JsonProperty("id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Picture { get; set; }
        public string Email { get; set; }
        /// <summary>
        /// Used by CosmosDB to partition container items to improve querying performance.
        /// The value should be the first character of the id.
        /// </summary>
        public string PartitionKey { get; set; }
        public DateTime Created { get; set; }
        /// <summary>
        /// If this user registered in the Apollo era then we have a way to link their identity back to the IDP beyond just email addresses which are not immutable.
        /// </summary>
        public string LegacyApolloId { get; set; }
        #endregion

        #region constructors
        public User()
        {
            Created = DateTime.Now;
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
                string.IsNullOrEmpty(Email) ||
                string.IsNullOrEmpty(PartitionKey))
                return false;

            return true;
        }
        #endregion
    }
}
