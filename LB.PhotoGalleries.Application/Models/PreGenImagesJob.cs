namespace LB.PhotoGalleries.Application.Models
{
    public class PreGenImagesJob
    {
        public Image Image { get; set; }
        public byte[] ImageBytes { get; set; }

        public PreGenImagesJob(Image image, byte[] imageBytes)
        {
            Image = image;
            ImageBytes = imageBytes;
        }
    }
}
