# PhotoGalleries

A modern photo gallery platform showcasing photography from [londonbikers.com](https://londonbikers.com). Built with ASP.NET Core, this application provides a fast, secure, and feature-rich experience for photographers and viewers alike.

**Live Site:** [photos.londonbikers.com](https://photos.londonbikers.com)

## Tech Stack

- **ASP.NET Core 9.0** - Modern web framework with MVC pattern
- **Azure Cosmos DB** - NoSQL database for scalable data storage
- **Azure Blob Storage** - Photo storage with CDN integration
- **Azure CDN** - Global content delivery for fast image loading
- **ImageFlow** - High-performance dynamic image resizing ([imageflow.io](https://www.imageflow.io))
- **OpenID Connect** - Federated authentication with IdentityServer
- **GitHub Actions** - Automated CI/CD deployment to VPS servers

## Features

### User Features
- Browse photo galleries organized by category
- View high-quality photos with dynamic resizing for optimal performance
- See detailed photo metadata (camera settings, EXIF data)
- Browse photos by tags (extracted from metadata)
- Comment on individual photos and galleries
- Fast, responsive design

### Admin Features
- Create and manage galleries
- Organize galleries into categories
- Upload and manage photos
- Automatic metadata extraction
- Tag management
- HTML content support with sanitization

### Security
- OpenID Connect authentication
- HTML sanitization to prevent XSS attacks
- File signature validation for uploads
- Secure authorization for private galleries
- Rate limiting and resource exhaustion protection

## Architecture

The application follows a clean architecture pattern:

- **LB.PhotoGalleries** - ASP.NET Core MVC web application
- **LB.PhotoGalleries.Application** - Business logic and services
- **LB.PhotoGalleries.Models** - Domain models and entities
- **LB.PhotoGalleries.Shared** - Shared utilities and helpers
- **LB.PhotoGalleries.Worker** - Background processing for image operations

## Development

### Dev container (recommended)

The repo includes a [dev container](https://containers.dev) (`.devcontainer/`) that provides .NET 9 SDK, PowerShell and the C# tooling on Ubuntu 24.04, matching the Linux VPS the app runs on. There are no emulators: the container talks to the shared dev Cosmos DB and Storage account.

1. Install Docker Desktop and the VS Code **Dev Containers** extension.
2. Open the repo in VS Code and run **Dev Containers: Reopen in Container**.
   For faster file I/O on Windows, use **Dev Containers: Clone Repository in Container Volume** instead.
3. Load secrets once (they're kept in a Docker volume, so they survive container rebuilds):
   ```bash
   pwsh ./Add-Secrets.ps1 -SourceFile /path/to/web-secrets.json -Project Web
   pwsh ./Add-Secrets.ps1 -SourceFile /path/to/worker-secrets.json -Project Worker
   ```
   Make sure any path settings (e.g. `ImageFlow:DiskCachePath`, `Logging:Path`) are Linux paths.
4. Use the **.NET Core Launch (web)** or **.NET Core Launch (Worker)** debug configurations. The site is forwarded to `https://localhost:44343`. The container's dev certificate isn't trusted by your host browser, so expect a certificate warning the first time.

### Prerequisites (without the dev container)
- .NET 9.0 SDK
- Access to the dev Cosmos DB and Azure Storage account

### Getting Started

```bash
# Clone the repository
git clone https://github.com/londonbikers/PhotoGalleries.git
cd PhotoGalleries

# Restore dependencies
dotnet restore

# Run the application
cd LB.PhotoGalleries
dotnet run
```

### Configuration

Secrets are supplied via `dotnet user-secrets`. `Add-Secrets.ps1` flattens a JSON file into user-secrets for the Web or Worker project. The keys the app reads include:

```json
{
  "CosmosDB": { "Uri": "...", "PrimaryKey": "...", "DatabaseName": "PhotoGalleries" },
  "Storage": { "ConnectionString": "..." },
  "Authentication": { "Authority": "...", "ClientId": "...", "ClientSecret": "...", "CloseSiteToThePublic": "false" }
}
```

See `LB.PhotoGalleries.Worker/appsettings.json` for the Worker's full set.

## Deployment

The application uses GitHub Actions for automated deployment:

- **TEST**: Automatically deploys on push to `master` branch
- **PROD**: Manual deployment via GitHub Actions workflow

Both environments are hosted on VPS servers running Ubuntu with .NET 9.0 runtime.

## URL Structure

| Path | Description |
|---|---|
| `/` | Latest galleries and introduction |
| `/categories` | List of all categories |
| `/categories/{name}` | Category detail page with galleries |
| `/galleries/{id}/{name}` | Gallery detail with photos |
| `/admin` | Admin dashboard |
| `/admin/categories` | Manage categories |
| `/admin/galleries` | Manage galleries |
| `/admin/galleries/{id}` | Edit gallery |

## Contributing

This is a private project for londonbikers.com, but we welcome bug reports and feature suggestions via GitHub Issues.

## License

All rights reserved. This code is proprietary to londonbikers.com.
