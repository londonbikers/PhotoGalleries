# Security & Update Checklist

This document tracks security issues, dependency updates, and technical improvements identified in the PhotoGalleries application review.

## 🚨 IMMEDIATE ACTION REQUIRED

### Framework & Support
- [x] **Upgrade to .NET 9.0** - .NET 6.0 is out of support (EOL: November 12, 2024)
  - [x] Update all `.csproj` files from `net6.0` to `net9.0`
  - [x] Test application thoroughly in dev environment
  - [ ] Update CI/CD pipelines
  - [ ] Update Azure Web App runtime stack

### Critical Security Vulnerabilities

- [x] **Fix Authorization in SetPosition API** (HIGH PRIORITY)
  - Location: `LB.PhotoGalleries/Controllers/API/ImagesController.cs:24-37`
  - Issue: Any photographer can modify images in galleries they don't own
  - Fix: Added per-object authorization checks to verify user owns the gallery
  - Also fixed: AddTag, AddTags, and RemoveTag methods had the same vulnerability

- [ ] **Fix Cross-Site Scripting (XSS) Vulnerabilities** (HIGH PRIORITY)
  - [ ] Add HTML encoding for `Image.Name` in views
  - [ ] Add HTML encoding for `Image.Caption` in views (Details.cshtml:93)
  - [ ] Add HTML encoding for `Image.Credit` in views
  - [ ] Replace `Html.Raw()` for Gallery descriptions with sanitized rendering
  - [ ] Add HTML encoding for `Comment.Text`
  - [ ] Implement allowlist-based HTML sanitization library (e.g., HtmlSanitizer)

## 🔴 HIGH PRIORITY

### Security Issues

- [ ] **Add File Signature Validation**
  - Location: `LB.PhotoGalleries/Controllers/Admin/ImagesController.cs:307-308`
  - Add magic number validation, don't rely solely on Content-Type header
  - Verify file signatures match expected image formats

- [ ] **Reduce ImageFlow Size Limits**
  - Location: `LB.PhotoGalleries/Startup.cs:175-177`
  - Change from 99999x99999 to reasonable limits (e.g., 8000x8000)
  - Document why limits were chosen

- [ ] **Fix Watermark Bypass Vulnerability**
  - Location: `LB.PhotoGalleries.Application/ImageResizing.cs:56`
  - Don't rely on Referer header (easily spoofed)
  - Use authenticated session or other server-side mechanism

- [ ] **Validate User Picture URLs**
  - Location: `LB.PhotoGalleries.Application/UserManagement.cs:68`
  - Add URL scheme validation (only allow https://)
  - Validate Content-Type of downloaded content
  - Add file size limits

### Dependency Updates (High Priority)

- [ ] **Update Authentication Packages**
  - [ ] `Microsoft.AspNetCore.Authentication.OpenIdConnect` 6.0.29 → 9.0.x
  - [ ] Test authentication flow thoroughly after update

- [ ] **Update Configuration Packages**
  - [ ] `Microsoft.Extensions.Configuration` 6.0.1 → 9.0.x
  - [ ] `Microsoft.Extensions.Configuration.CommandLine` 6.0.0 → 9.0.x
  - [ ] `Microsoft.Extensions.Configuration.EnvironmentVariables` 6.0.1 → 9.0.x
  - [ ] `Microsoft.Extensions.Configuration.Json` 6.0.0 → 9.0.x
  - [ ] `Microsoft.Extensions.Configuration.UserSecrets` 6.0.1 → 9.0.x

## 🟡 MEDIUM PRIORITY

### Security Issues

- [ ] **Add CSRF Protection to API Endpoints**
  - [ ] Add `[ValidateAntiForgeryToken]` to ImagesController API methods
  - [ ] Add `[ValidateAntiForgeryToken]` to GalleriesController API methods
  - [ ] Update JavaScript to include anti-forgery tokens in AJAX calls

- [ ] **Fix Comment Deletion Integrity**
  - Location: `LB.PhotoGalleries/Controllers/API/ImagesController.cs:191-208`
  - Add validation that comment belongs to specified image
  - Add integrity checks

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

- [ ] **Update Azure SDK Packages**
  - [ ] `Azure.Storage.Blobs` 12.19.1 → 12.26.0
  - [ ] `Azure.Storage.Queues` 12.17.1 → 12.24.0
  - [ ] `Microsoft.Azure.Cosmos` 3.39.1 → 3.55.0

- [ ] **Update Serilog Packages**
  - [ ] `Serilog` 3.1.1 → 4.3.0
  - [ ] `Serilog.AspNetCore` 6.1.0 → 9.0.0
  - [ ] `Serilog.Enrichers.Environment` 2.3.0 → 3.0.1
  - [ ] `Serilog.Sinks.ApplicationInsights` 4.0.0 → 4.1.0
  - [ ] `Serilog.Sinks.Async` 1.5.0 → 2.1.0
  - [ ] `Serilog.Sinks.Console` 5.0.1 → 6.1.1
  - [ ] `Serilog.Sinks.Debug` 2.0.0 → 3.0.0
  - [ ] `Serilog.Sinks.File` 5.0.0 → 7.0.0

- [ ] **Update Application Insights**
  - [ ] `Microsoft.ApplicationInsights.AspNetCore` 2.22.0 → 2.23.0

- [ ] **Update Other Packages**
  - [ ] `Imageflow.Server` 0.8.3 → 0.9.0
  - [ ] `Imageflow.Server.HybridCache` 0.8.3 → 0.9.0
  - [ ] `Imageflow.Server.Storage.AzureBlob` 0.8.3 → 0.9.0
  - [ ] `Imageflow.Net` 0.13.1 → 0.14.1
  - [ ] `Newtonsoft.Json` 13.0.3 → 13.0.4
  - [ ] `MetadataExtractor` 2.8.1 → 2.9.0

## 🟢 LOW PRIORITY

### Dependency Updates

- [ ] **Update Testing Packages**
  - [ ] `Microsoft.NET.Test.Sdk` 17.6.0 → 18.0.1
  - [ ] `xunit` 2.4.2 → 2.9.3
  - [ ] `xunit.runner.visualstudio` 2.4.5 → 3.1.5
  - [ ] `coverlet.collector` 6.0.0 → 6.0.4

- [ ] **Update Other Dependencies**
  - [ ] `Spectre.Console` 0.49.0 → 0.54.0
  - [ ] `System.Data.SqlClient` 4.8.6 → 4.9.0
  - [ ] `Microsoft.AspNetCore.Session` 2.2.0 → 2.3.0

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
- Last Updated: 2025-01-22
- Next Review: _TBD_

### Notes
- .NET 6.0 reached end of support on November 12, 2024
- No known CVEs in current dependencies (as of review date)
- Application shows good engineering practices overall
- Focus on security hardening before production deployment
