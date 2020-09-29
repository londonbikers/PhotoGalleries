namespace LB.PhotoGalleries.Models
{
    public class ProcessImageResponse
    {
        public string FileSpec { get; }
        public string StorageId { get; }

        public ProcessImageResponse(string fileSpec, string storageId)
        {
            FileSpec = fileSpec;
            StorageId = storageId;
        }
    }
}
