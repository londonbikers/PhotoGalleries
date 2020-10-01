namespace LB.PhotoGalleries.Models
{
    /// <summary>
    /// The most efficient way to retrieve an object from the database is to perform a point read.
    /// To do this a partition key and document id are needed. This object helps encapsulates those for any type of object.
    /// </summary>
    public class DatabaseId
    {
        public string Id { get; set; }
        public string PartitionKey { get; set; }

        public DatabaseId(string id, string partitionKey)
        {
            Id = id;
            PartitionKey = partitionKey;
        }
    }
}
