using LB.PhotoGalleries.Application.Servers;

namespace LB.PhotoGalleries.Application
{
    /// <summary>
	/// The application entry point. Employs a singleton pattern.
	/// </summary>
    public class Server
    {
        #region accessors
        /// <summary>
        /// Retrieves the single instance of the application.
        /// </summary>
        public static Server Instance { get; }
        public CategoryServer Categories { get; internal set; }
        public GalleryServer Galleries { get; internal set; }
        #endregion

        #region constructors
        static Server()
        {
            Instance = new Server
            {
                Categories = new CategoryServer(),
                Galleries = new GalleryServer()
            };
        }
        #endregion
    }
}