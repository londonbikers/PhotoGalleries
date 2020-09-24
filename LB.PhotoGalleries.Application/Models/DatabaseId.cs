namespace LB.PhotoGalleries.Application.Models
{
    /// <summary>
    /// The most efficient way to retrieve an object from the database is to perform a point read.
    /// To do this a partition key and document id are needed. This object helps encapsulates those for any type of object.
    /// </summary>
    internal class DatabaseId
    {
        internal string Id { get; set; }
        internal string PartitionKey { get; set; }
    }
}
