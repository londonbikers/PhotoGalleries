namespace LB.PhotoGalleries.Models
{
    public class ProcessImageInput
    {
        public string ImageId { get; }
        public string FileSpec { get; }
        public byte[] ImageBytes { get; }

        public ProcessImageInput(string imageId, byte[] imageBytes, string fileSpec)
        {
            ImageId = imageId;
            ImageBytes = imageBytes;
            FileSpec = fileSpec;
        }
    }
}
