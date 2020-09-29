using LB.PhotoGalleries.Models.Enums;

namespace LB.PhotoGalleries.Models
{
    public class ProcessImageInput
    {
        public Image Image { get; }
        public FileSpec FileSpec { get; }
        public byte[] ImageBytes { get; }

        public ProcessImageInput(Image image, byte[] imageBytes, FileSpec fileSpec)
        {
            Image = image;
            ImageBytes = imageBytes;
            FileSpec = fileSpec;
        }
    }
}
