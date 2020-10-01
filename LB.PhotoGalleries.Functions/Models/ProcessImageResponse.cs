using LB.PhotoGalleries.Models.Enums;

namespace LB.PhotoGalleries.Functions.Models
{
    public class ProcessImageResponse
    {
        public FileSpec FileSpec { get; }
        public string StorageId { get; }

        public ProcessImageResponse(FileSpec fileSpec, string storageId)
        {
            FileSpec = fileSpec;
            StorageId = storageId;
        }
    }
}
