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
  - [x] Test authentication flow thoroughly after update - Completed, no issues found

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
  - ⚠️ **This sweep covered API controllers only.** CodeQL later found two unprotected POST endpoints
    in **MVC** controllers that it never reached — `AccountController.EmailPreferences` and
    `Areas/Admin/Controllers/ImagesController.Upload`. Both fixed 2026-08-08, see below. The original
    statement was accurate as written but read as broader coverage than it had.

- [x] **Fix Comment Deletion Integrity**
  - Location: `LB.PhotoGalleries/Controllers/API/ImagesController.cs:204-238` and `LB.PhotoGalleries/Controllers/API/GalleriesController.cs:34-60`
  - Added validation that image belongs to specified gallery
  - Added validation that gallery belongs to specified category
  - Added null checks for gallery and image
  - Prevents manipulation of request parameters to delete comments from unrelated images/galleries

- [ ] **Add Rate Limiting** (DEFERRED)
  - Decision: Not implementing at this time
  - Rationale: Low traffic volume, CSRF protection already in place, can revisit if abuse occurs

- [ ] **Reduce Upload Size Limits** (NOT IMPLEMENTING)
  - Location: `LB.PhotoGalleries/Controllers/Admin/ImagesController.cs:281-282`
  - Decision: Keeping 100MB limit as-is
  - Rationale: Ability to handle very large professional photos is a feature strength
  - Current limit appropriate for professional photography use case

- [x] **Add Cookie SameSite Attribute**
  - Location: `LB.PhotoGalleries/Startup.cs:40-46` and `LB.PhotoGalleries/Startup.cs:67-72`
  - Added `SameSite = SameSiteMode.Lax` to session cookies
  - Added `SameSite = SameSiteMode.Lax` to authentication cookies
  - Also added `HttpOnly` and `SecurePolicy.Always` to authentication cookies
  - Using Lax instead of Strict to ensure OAuth/OIDC authentication flows work correctly

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
  - [x] `Imageflow.Net` 0.13.1 → 0.14.1 (⚠️ only applied to Worker at the time; Models was missed and
        stayed on 0.13.1 — corrected 2026-08-08, see below)
  - [x] `Newtonsoft.Json` 13.0.3 → 13.0.4 (⚠️ previously recorded as "not found in solution", but it is
        referenced by Models — applied 2026-08-08)
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
  - [x] `Microsoft.AspNetCore.Session` — reference removed entirely 2026-08-08. Session ships in the
        `Microsoft.AspNetCore.App` shared framework on net9.0, so the legacy netstandard2.0 package was
        redundant. `AddSession()`/`UseSession()` resolve without it.

## 🔍 CODEQL TRIAGE — 2026-08-08

CodeQL had been in `disabled_inactivity` state since 2025-11-23 and was re-enabled during the dependency
pass. Its first successful scan raised 14 alerts. Full triage: **3 real, 11 noise.**

### Fixed

- [x] **Add CSRF protection to `AccountController.EmailPreferences`** (POST)
  - Added `[ValidateAntiForgeryToken]`. The view already emitted the token via `Html.BeginForm()`, so
    no view change was needed.
  - Impact was low — an attacker could flip a victim's comment-notification preference.

- [x] **Add CSRF protection to `Areas/Admin/Controllers/ImagesController.Upload`** (POST)
  - Added `[ValidateAntiForgeryToken]`, **plus a matching `headers` entry in the Dropzone config** in
    `Areas/Admin/Views/Galleries/Edit.cshtml`.
  - ⚠️ **The two changes are inseparable.** `site.js` attaches `X-CSRF-TOKEN` through `$.ajaxSetup`,
    which covers jQuery AJAX only. Dropzone uses `XMLHttpRequest` directly, so adding the attribute
    on its own would have made every admin image upload fail with a 400. Anyone reverting one of
    these must revert both.
  - Root cause of the gap: the original CSRF sweep covered API controllers, and this is an MVC
    controller in an Area.

- [x] **Add security headers** — `X-Frame-Options: SAMEORIGIN`, `X-Content-Type-Options: nosniff`,
      `Referrer-Policy: strict-origin-when-cross-origin`
  - Implemented as middleware in `Startup.cs`, placed before `UseHttpsRedirection()` so it covers
    static files and ImageFlow output as well as MVC responses. `Startup.cs` previously had no
    security headers at all beyond `UseHsts()`, so the site was framable.
  - Also added to `web.config` for IIS parity. Note **`web.config` is inert in production** — the app
    runs behind Kestrel on a Linux VPS — so the middleware is the functional fix. The duplication
    exists because the CodeQL rule inspects `web.config` specifically.
  - `SAMEORIGIN` rather than `DENY`, so our own pages can still frame each other.
  - Content-Security-Policy deliberately **not** added: the views carry a lot of inline script and a
    CSP strict enough to be worth having would break them. Worth doing as its own piece of work.

### Dismissed as false positives

| Alert | Rule | Why |
|---|---|---|
| #86 | `cs/user-controlled-bypass` | `Api/ImagesController.cs:289` is `if (!tags.Contains(',')) return BadRequest(...)` — input validation, not a security guard. Real authorisation (`CanUserEditObject`) happens afterwards and is not user-controlled. |
| #21, #26, #27 | `cs/web/xss` | All three are `asp-route-returnUrl="@Context.Request.Path"`. Razor tag helpers URL-encode route values. The sink was also checked: `HomeController.SignIn` uses `LocalRedirect()`, which throws on non-local URLs, so there is no open redirect either. |
| #89 | `cs/web/missing-x-frame-options` | Duplicate raised against `bin/Debug/net9.0/web.config`, a build artifact. |
| 11 JS alerts | various | All in vendored `wwwroot/lib` third-party code. |

### Scan configuration

`.github/codeql/codeql-config.yml` excludes `wwwroot/lib`, `**/bin` and `**/obj`. Excluding the vendored
JavaScript does **not** make it safe — see the front-end library note in TRACKING below.

## 📦 DEPENDENCY UPDATE PASS — 2026-08-08

Scope: bring packages current **within the .NET 9 line**. All projects remain on `net9.0`; no target
framework changed.

### Completed

- [x] **Enable Dependabot version updates** — added `.github/dependabot.yml` (NuGet + GitHub Actions,
      weekly). Previously absent, so only *security* updates ran; version updates require the config
      file. This is why package versions had drifted across the solution.
- [x] `HtmlSanitizer` 9.0.886 → 9.1.982 — resolves GHSA-j92c-7v7g-gj3f (template tag sanitisation
      bypass). Supersedes Dependabot PR #69, which proposed the narrower 9.0.892 fix.
      Note: `Helpers.SanitiseHtml()` uses a strict allowlist that never permitted `template`, so the
      bypass was not exploitable in this application. Updated as defence in depth.
- [x] `Microsoft.Extensions.Configuration.*` 9.0.0 → 9.0.18 (7 projects)
- [x] `Microsoft.AspNetCore.Authentication.OpenIdConnect` 9.0.0 → 9.0.18
- [x] `Serilog` 4.3.0 → 4.4.0
- [x] `Serilog.Sinks.ApplicationInsights` 4.1.0 → 5.0.1
- [x] `Newtonsoft.Json` 13.0.3 → 13.0.4
- [x] `MetadataExtractor` 2.9.0 → 2.9.3
- [x] `System.Data.SqlClient` 4.9.0 → 4.9.1
- [x] `Mailjet.Api` 3.0.0 → 3.0.1
- [x] `Microsoft.NET.Test.Sdk` 18.0.1 → 18.8.1
- [x] `coverlet.collector` 6.0.4 → 10.0.1
- [x] `Microsoft.Azure.Cosmos` 3.55.0 → 3.62.1 *(isolated in its own commit — largest blast radius)*
- [x] `Imageflow.Net` (Models) 0.13.1 → 0.14.1 — see "Advisories resolved" below
- [x] Removed redundant `Microsoft.AspNetCore.Session` 2.2.0 reference

### Advisories resolved

Vulnerable package count went from **6 → 2** (`dotnet list package --vulnerable --include-transitive`):

| Package | Severity | How resolved |
|---|---|---|
| `HtmlSanitizer` 9.0.886 | Moderate | → 9.1.982 |
| `AngleSharp` 0.17.1 | Moderate | transitively, via HtmlSanitizer |
| `System.Security.Cryptography.Xml` 4.5.0 | Moderate | transitively |
| `System.Text.Json` 6.0.9 | **High** | `Imageflow.Net` 0.13.1 → 0.14.1 in Models |

The `System.Text.Json` case is worth recording: Worker was already on `Imageflow.Net` 0.14.1 (which
pulls the patched `System.Text.Json` 6.0.11) and scanned clean, but Models was still on 0.13.1. Because
every other project reaches Imageflow *through* Models, that one stale reference propagated
GHSA-8g4q-xg66-9fp4 into seven projects.

### Resolved via `Mailjet.Api` 4.0.1

- [x] `System.Net.Http` 4.3.0 — **High**, GHSA-7jgj-8wvc-jh57
- [x] `System.Text.RegularExpressions` 4.3.0 — **High**, GHSA-cmhx-cq75-c4mj

Both arrived via `Mailjet.Api` 3.0.1 → `NETStandard.Library` 1.6.1. `Mailjet.Api` 4.0.1 targets
`netstandard2.0` and depends only on `Microsoft.CSharp` 4.7.0 and `Newtonsoft.Json` 13.0.4 — it drops
`NETStandard.Library` entirely.

> **Correction worth keeping.** These were initially recorded here as "no fix available, 3.0.1 is the
> latest Mailjet.Api". That was wrong — it came from a query scoped to major version 3, and the result
> was written up as fact. `Mailjet.Api` 4.0.1 existed the whole time, and Dependabot surfaced it within
> minutes of version updates being enabled. Before writing off a dependency as having no upgrade path,
> check across major versions.

**With this applied the solution has no vulnerable packages at all** (`dotnet list package --vulnerable
--include-transitive`), down from six at the start of the pass.

⚠️ **Untested at runtime, by decision.** 4.x is a major version bump. The call sites in
`LB.PhotoGalleries/Services/NotificationService.cs` (`MailjetClient`, `MailjetRequest`, `Send.Resource`,
`GetErrorMessage()`) all compile, CI passes, and the service starts — but **none of that exercises email
sending**, and a smoke test was consciously skipped on 2026-08-08 as not worth the effort. If
notification emails are later found not to arrive, this upgrade is the first place to look;
`Send.Resource` is the most likely culprit, since Mailjet's v3 and v3.1 send APIs differ.

### Deliberately held back

- [ ] `Azure.Storage.Blobs` 12.26.0 → 12.29.1 / `Azure.Storage.Queues` 12.24.0 → 12.27.1.
      **Blocked on the .NET 10 upgrade.** 12.29.1 raises the `Azure.Core` floor to 1.55.0, which depends
      on `Microsoft.Extensions.{Configuration,Hosting}.Abstractions` 10.0.3 and drags the solution onto
      the .NET 10 package line (NU1605 package downgrade errors). `Azure.Core` 1.47.3 — the version in
      use today — has no `Microsoft.Extensions` dependencies at all. No known advisories affect the
      pinned versions. Cosmos is unaffected: it floors `Azure.Core` at 1.44.1 in both 3.55.0 and 3.62.1.
- [ ] `Microsoft.ApplicationInsights.AspNetCore` 2.23.0 → 3.1.2. Pulls
      `Microsoft.ApplicationInsights` 3.x, which `Serilog.Sinks.ApplicationInsights` 5.0.1 excludes
      (requires `>= 2.23.0 && < 3.0.0`). Blocked until the Serilog sink supports App Insights 3.x.
- [ ] `Imageflow.Net` → 0.15.1 and `Spectre.Console` 0.54.0 → 0.57.2. Both 0.x, where minor releases are
      breaking by convention. Needs a real image-processing smoke test rather than a compile check.
- [ ] `Microsoft.Extensions.*`, `Microsoft.AspNetCore.*` and `Serilog.AspNetCore` 10.x — require
      `net10.0`. Dependabot is configured to suppress these majors until the framework upgrade.

### ⚠️ CI workflows are stale

The GitHub Actions workflows have drifted badly and need attention independently of this pass:

- [ ] `github/codeql-action` **v1** — deprecated and disabled by GitHub; `codeql-analysis.yml` is
      likely failing on every run. Needs v3.
- [ ] `actions/checkout` v2 → v5, and one workflow pinned to `@master`
- [ ] `actions/setup-dotnet` v1 → v5
- [ ] `appleboy/scp-action` and `appleboy/ssh-action` float unpinned on `@master`

The new Dependabot `github-actions` ecosystem will raise these, but the CodeQL v1 → v3 jump may need
manual intervention.

### Verification

`dotnet restore` clean (no NU1605/NU1608/NU1902) · Release build 0 errors · 6/6 tests pass.

Note the test suite is only 6 tests, so the build is doing nearly all the verification work. The Cosmos
3.62.1 bump in particular has only been compile-and-resolve checked and has **not** been exercised
against a live Cosmos instance.

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
- Last Updated: 2026-08-08 (dependency update pass)
- Next Review: _TBD_

### Notes
- .NET 9.0 upgrade completed and deployed to production (2025-01-23)
- Worker VPS upgraded to .NET 9.0 runtime (2025-01-23)
- All immediate and high-priority security vulnerabilities addressed
- All medium-priority security issues completed
- Both TEST and PROD environments running .NET 9.0 successfully
- Authentication flow tested and working correctly after OpenIdConnect package update
- Rate limiting deferred - current traffic volume doesn't warrant implementation
- Upload size limits kept at 100MB - appropriate for professional photography use case
- As of 2026-08-08 the solution has **no vulnerable NuGet packages and no open Dependabot alerts**, down
  from six advisories at the start of that pass. Note the 2025-01-23 entry claiming "no known CVEs" had
  since gone stale without anyone noticing — the claim is only as good as its date.
- Dependabot version updates were not enabled until 2026-08-08; only security updates ran before then,
  which is why dependencies drifted between reviews. Enabling them immediately surfaced the Mailjet 4.x
  upgrade that a manual review had missed — worth remembering the next time a dependency is written off
  as having no upgrade path.
- CodeQL was in `disabled_inactivity` state and had not run since 2025-11-23. Re-enabled 2026-08-08; its
  first successful scan raised 14 alerts (6 high, 8 medium), 11 of them in vendored `wwwroot/lib`
  JavaScript. See below.
- ⚠️ `wwwroot/lib` front-end libraries have **no update mechanism at all** — no `package.json`, hand-copied
  in, invisible to Dependabot. `moment.js` is also deprecated upstream. This is the largest remaining
  unmanaged dependency surface.
- .NET 10 upgrade is the next significant piece of framework work, and now gates the Azure.Storage,
  Serilog.AspNetCore and Microsoft.Extensions package updates. .NET 9 (STS) support ends 2026-11-10.
- Application shows good engineering practices overall
- Remaining work: .NET 10 upgrade, vendored front-end libraries, low-priority
  technical debt and architectural improvements
