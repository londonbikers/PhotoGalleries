**Notes**

The repository is a multi‑project ASP.NET Core solution for hosting photo galleries. It includes the main MVC website, data models, application logic, background services, and several small command‑line tools for maintenance (migration, comment counting, etc.). The README outlines the technology stack and expected site features such as galleries, categories, metadata display, tagging, user comments, and OpenID Connect authentication.

**Summary**

- **Solution Layout** – The LB.PhotoGalleries.sln file references several projects: the MVC site (LB.PhotoGalleries), the application layer (LB.PhotoGalleries.Application), data models (LB.PhotoGalleries.Models), shared utilities (LB.PhotoGalleries.Shared), a background worker (LB.PhotoGalleries.Worker), migrator, comparison tool, and auxiliary utilities such as a comment counter and metadata printer.

- **Application Core** – LB.PhotoGalleries.Application implements a singleton Server class to manage Cosmos DB, Azure Blob storage, and Azure Queues. It exposes servers for categories, galleries, images, and users, and initializes storage/queues on startup.

- **Domain Models** – Located in LB.PhotoGalleries.Models, classes like Gallery, Image, Category, and User capture gallery data, comments, metadata, and user info. For instance, Gallery holds a list of Comment objects and a CommentCount field to track totals. Images store metadata and pre-generated file IDs for multiple resolutions.

- **Shared Utilities** – The LB.PhotoGalleries.Shared project provides helpers such as extension methods for strings and common utilities for handling images and tag CSVs.

- **Web Site** – The MVC application configures authentication via OpenID Connect and sets up ImageFlow for dynamic image resizing with disk caching when enabled. Routes map to categories, galleries, images, and admin pages. A hosted NotificationService processes a queue to send comment email notifications via Mailjet.

- **Background Worker** – LB.PhotoGalleries.Worker listens to an image-processing queue. It generates resized image files, extracts metadata, updates the database, and pre-caches images to improve site performance. Metadata extraction is handled in MetadataUtils.ParseAndAssignImageMetadataAsync, which reads EXIF/IPTC/XMP data and populates the Image object.

- **Supporting Tools** – Additional console apps assist with migration (LB.PhotoGalleries.Migrator), tag conversion, comment counting, and more. For example, LB.PhotoGalleries.Migrator reads legacy SQL data and populates Cosmos DB using the shared Server infrastructure.

Overall, the repository provides:

1. **MVC Web App** for browsing and managing galleries.

1. **Application Layer** (Server + Servers) abstracting storage and database operations.

1. **Background Worker & Notification Service** using Azure Storage queues for image processing and email notifications.

1. **Domain Models and Utilities** for handling galleries, images, and users.

1. **Supporting Tools** for migration and maintenance tasks.

**Pointers for further learning**

- **Azure Cosmos DB SDK:** Many operations use GetItemQueryIterator, partition keys, and custom indexes (see Indexes/Galleries.json and Indexes/Images.json for composite indexes). Understanding Cosmos DB querying and throughput is helpful.

- **ImageFlow:** The site relies on ImageFlow middleware and watermark handlers for resizing images on the fly (ImageResizing.cs).

- **Queues & Background Services:** Both the worker and NotificationService demonstrate how Azure Storage queues are polled and messages processed asynchronously.

- **Authentication and Claims: **The Startup configuration shows integration with an IdentityServer via OpenID Connect and uses claims to populate user records.

- **Web UI:** Explore the Controllers and Razor Views under LB.PhotoGalleries to see how categories, galleries, and images are rendered.

This structure gives you a broad view of how the site handles gallery data, image processing, and user interactions while leveraging Azure services for scalability. Further investigation of individual controllers, Razor pages, and console utilities will deepen your understanding of the full workflow from uploads to user comments and notifications.
