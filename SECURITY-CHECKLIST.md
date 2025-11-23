# Security & Update Checklist

This document tracks security issues, dependency updates, and technical improvements identified in the PhotoGalleries application review.

## 🚨 IMMEDIATE ACTION REQUIRED

### Framework & Support
- [x] **Upgrade to .NET 9.0** - .NET 6.0 is out of support (EOL: November 12, 2024)
  - [x] Update all `.csproj` files from `net6.0` to `net9.0`
  - [x] Test application thoroughly in dev environment
  - [x] Update CI/CD pipelines (GitHub Actions workflows updated to .NET 9.0)
  - [x] Install .NET 9.0 runtime on VPS servers (both TEST and PROD servers now running .NET 9.0)

### Critical Security Vulnerabilities

- [x] **Fix Authorization in SetPosition API** (HIGH PRIORITY)
  - Location: `LB.PhotoGalleries/Controllers/API/ImagesController.cs:24-37`
  - Issue: Any photographer can modify images in galleries they don't own
  - Fix: Added per-object authorization checks to verify user owns the gallery
  - Also fixed: AddTag, AddTags, and RemoveTag methods had the same vulnerability

- [x] **Fix Cross-Site Scripting (XSS) Vulnerabilities** (HIGH PRIORITY)
  - [x] Add HTML encoding for `Image.Name` in views
  - [x] Add HTML encoding for `Image.Caption` in views (Details.cshtml:93)
  - [x] Add HTML encoding for `Image.Credit` in views
  - [x] Replace `Html.Raw()` for Category descriptions with sanitised rendering
  - [x] Add HTML encoding for `Comment.Text` (user comments remain fully encoded)
  - [x] Implemented HtmlSanitizer library for photographer/admin content (Image.Name, Caption, Credit, Category.Description)
    - Allows safe HTML tags (p, br, strong, b, em, i, u, a) whilst preventing XSS
    - Preserves existing HTML formatting from legacy data
    - User-generated comments remain fully HTML-encoded for maximum security

## 🔴 HIGH PRIORITY

### Security Issues

- [x] **Add File Signature Validation**
  - Location: `LB.PhotoGalleries/Controllers/Admin/ImagesController.cs:30` and `LB.PhotoGalleries/Controllers/API/ImagesController.cs:340`
  - Added magic number validation for JPEG (FF D8 FF) and PNG (89 50 4E 47 0D 0A 1A 0A)
  - Created Helpers.ValidateImageFileSignature() method to verify actual file content
  - Validates file signatures before upload/replace operations
  - Prevents malicious files disguised as images with spoofed Content-Type headers

- [x] **Reduce ImageFlow Size Limits**
  - Location: `LB.PhotoGalleries/Startup.cs:179-181`
  - Changed from 99999x99999 to 16000x16000
  - Limit accommodates professional cameras (e.g., Phase One IQ4 150MP: 14204x10652)
  - Allows for panoramas and stitched images
  - Prevents DoS attacks from requesting massive image resizes (256MP limit)

- [x] **Validate User Picture URLs**
  - Location: `LB.PhotoGalleries.Application/Servers/UserServer.cs:190-269`
  - Added URL scheme validation (only HTTPS allowed)
  - Added Content-Type validation (only image/jpeg and image/png)
  - Added file size limit (5MB maximum)
  - Added file signature validation using magic numbers
  - Added 30-second timeout to prevent hanging requests
  - Prevents SSRF attacks, resource exhaustion, and malicious file downloads

### Dependency Updates (High Priority)

- [x] **Update Authentication Packages**
  - [x] `Microsoft.AspNetCore.Authentication.OpenIdConnect` 6.0.29 → 9.0.0
  - [ ] Test authentication flow thoroughly after update

- [x] **Update Configuration Packages**
  - [x] `Microsoft.Extensions.Configuration` 6.0.1 → 9.0.0
  - Note: CommandLine, EnvironmentVariables, Json, and UserSecrets are transitive dependencies automatically updated by ASP.NET Core 9.0

## 🟡 MEDIUM PRIORITY

### Security Issues

- [x] **Add CSRF Protection to API Endpoints**
  - [x] Add `[ValidateAntiForgeryToken]` to ImagesController API methods
  - [x] Add `[ValidateAntiForgeryToken]` to GalleriesController API methods
  - [x] Update JavaScript to include anti-forgery tokens in AJAX calls
  - Implementation: Added anti-forgery configuration in [Startup.cs:50-55](LB.PhotoGalleries/Startup.cs#L50-L55)
  - Tokens included in all pages via @Html.AntiForgeryToken() in layouts
  - jQuery configured to automatically send X-CSRF-TOKEN header with all AJAX requests
  - All API POST, PUT, DELETE endpoints protected with [ValidateAntiForgeryToken]

- [x] **Fix Comment Deletion Integrity**
  - Location: `LB.PhotoGalleries/Controllers/API/ImagesController.cs:204-238` and `LB.PhotoGalleries/Controllers/API/GalleriesController.cs:34-60`
  - Added validation that image belongs to specified gallery
  - Added validation that gallery belongs to specified category
  - Added null checks for gallery and image
  - Prevents manipulation of request parameters to delete comments from unrelated images/galleries

- [ ] **Add Rate Limiting**
  - [ ] Implement rate limiting on comment creation endpoints
  - [ ] Consider adding CAPTCHA for public comment forms
  - [ ] Add rate limiting middleware to API endpoints

- [ ] **Reduce Upload Size Limits**
  - Location: `LB.PhotoGalleries/Controllers/Admin/ImagesController.cs:281-282`
  - Consider reducing from 100MB to more reasonable limit
  - Implement per-user quota system

- [ ] **Add Cookie SameSite Attribute**
  - Location: `LB.PhotoGalleries/Startup.cs:40-45`
  - Add `SameSite = SameSiteMode.Strict` or `Lax` to session cookies

### Dependency Updates (Medium Priority)

- [x] **Update Azure SDK Packages**
  - [x] `Azure.Storage.Blobs` 12.19.1 → 12.26.0
  - [x] `Azure.Storage.Queues` 12.17.1 → 12.24.0
  - [x] `Microsoft.Azure.Cosmos` 3.39.1 → 3.55.0

- [x] **Update Serilog Packages**
  - [x] `Serilog` 3.1.1 → 4.3.0
  - [x] `Serilog.AspNetCore` 6.1.0 → 9.0.0
  - [x] `Serilog.Enrichers.Environment` 2.3.0 → 3.0.1
  - [x] `Serilog.Sinks.ApplicationInsights` 4.0.0 → 4.1.0
  - [x] `Serilog.Sinks.Async` 1.5.0 → 2.1.0
  - [x] `Serilog.Sinks.Console` 5.0.1 → 6.1.1
  - [x] `Serilog.Sinks.Debug` 2.0.0 → 3.0.0
  - [x] `Serilog.Sinks.File` 5.0.0 → 7.0.0

- [x] **Update Application Insights**
  - [x] `Microsoft.ApplicationInsights.AspNetCore` 2.22.0 → 2.23.0

- [x] **Update Other Packages**
  - [x] `Imageflow.Server` 0.8.3 → 0.9.0
  - [x] `Imageflow.Server.HybridCache` 0.8.3 → 0.9.0
  - [x] `Imageflow.Server.Storage.AzureBlob` 0.8.3 → 0.9.0
  - [x] `Imageflow.Net` 0.13.1 → 0.14.1
  - [ ] `Newtonsoft.Json` 13.0.3 → 13.0.4 (not found in solution)
  - [x] `MetadataExtractor` 2.8.1 → 2.9.0 (fixed breaking change: GetGeoLocation() → TryGetGeoLocation())

## 🟢 LOW PRIORITY

### Dependency Updates

- [x] **Update Testing Packages**
  - [x] `Microsoft.NET.Test.Sdk` 17.6.0 → 18.0.1
  - [x] `xunit` 2.4.2 → 2.9.3
  - [x] `xunit.runner.visualstudio` 2.4.5 → 3.1.5
  - [x] `coverlet.collector` 6.0.0 → 6.0.4

- [x] **Update Other Dependencies**
  - [x] `Spectre.Console` 0.49.0 → 0.54.0
  - [x] `System.Data.SqlClient` 4.8.6 → 4.9.0
  - [ ] `Microsoft.AspNetCore.Session` 2.2.0 → 2.3.0 (already on latest available)

## 🔧 TECHNICAL DEBT & CODE QUALITY

### Architecture Improvements

- [ ] **Replace Service Locator Pattern**
  - [ ] Refactor `Server.Instance` singleton to use dependency injection
  - [ ] Inject services directly into controllers
  - [ ] Update all consumers to accept dependencies via constructor

- [ ] **Create DTOs for API Responses**
  - [ ] Create response models separate from domain entities
  - [ ] Map domain models to DTOs in controllers
  - [ ] Don't expose internal domain structure to clients

- [ ] **Replace ViewData with ViewModels**
  - [ ] Create strongly-typed ViewModels for each view
  - [ ] Migrate away from untyped ViewData dictionary
  - [ ] Get compile-time safety for view data

- [ ] **Refactor Large Controllers**
  - [ ] Split `ImagesController` into focused controllers
  - [ ] Extract common logic into services
  - [ ] Reduce controller responsibilities

- [ ] **Extract Magic Strings to Constants**
  - [ ] Create constants class for role names ("Administrator", "Photographer")
  - [ ] Create constants for container names
  - [ ] Create constants for configuration keys
  - [ ] Create constants for queue names

### Performance & Scalability

- [ ] **Fix Blocking Startup**
  - Location: `LB.PhotoGalleries/Startup.cs:111`
  - Replace `Server.SetConfigurationAsync().Wait()` with proper async initialization
  - Add graceful degradation if database unavailable

- [ ] **Review Comment Storage Strategy**
  - Current: Comments stored as arrays in documents
  - Consider: Separate comments collection for better scalability
  - Analyze: Document size limits and query patterns

- [ ] **Add Query Result Caching**
  - Implement caching strategy for frequent queries
  - Cache user authorization checks (as noted in TODO)
  - Cache category and gallery listings

- [ ] **Review Cosmos DB Partition Strategy**
  - Document current partition key strategy
  - Analyze cross-partition queries
  - Optimize for cost and performance

### Code Quality

- [ ] **Add XML Documentation Comments**
  - Add documentation to public methods
  - Document expected behavior and exceptions
  - Enable XML documentation generation

- [ ] **Improve Error Handling**
  - Avoid exposing stack traces to users
  - Implement proper error pages
  - Log errors appropriately without leaking sensitive info

- [ ] **Remove Legacy ID Fields**
  - Evaluate if `LegacyNumId` and `LegacyGuidId` still needed
  - Plan migration if still required
  - Remove if migration complete

- [ ] **Complete Worker Integration**
  - Location: `LB.PhotoGalleries.Application/ImageServer.cs:88`
  - Move filename metadata extraction to worker
  - Complete async processing architecture

## 📊 TRACKING

### Review Dates
- Initial Review: 2025-01-22
- Last Updated: 2025-01-23
- Next Review: _TBD_

### Notes
- .NET 9.0 upgrade completed and deployed to production (2025-01-23)
- All immediate security vulnerabilities addressed
- Both TEST and PROD environments running .NET 9.0 successfully
- No known CVEs in current dependencies (as of review date)
- Application shows good engineering practices overall
- Focus remaining work on medium-priority security hardening and technical debt
