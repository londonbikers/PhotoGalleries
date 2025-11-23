using Imageflow.Fluent;
using Imageflow.Server;
using Imageflow.Server.HybridCache;
using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.IO;

namespace LB.PhotoGalleries;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        // add session so we can track some user events
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.IsEssential = true;
        });

        // make sure all our urls are generated in lower-case for purely aesthetic reasons
        services.Configure<RouteOptions>(o => o.LowercaseUrls = true);

        // configure anti-forgery for CSRF protection
        services.AddAntiforgery(options =>
        {
            // header name for AJAX requests to include the anti-forgery token
            options.HeaderName = "X-CSRF-TOKEN";
        });

        services.AddControllersWithViews();

        // reduces down the claims received
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "Cookies";
                options.DefaultChallengeScheme = "oidc";
            })
            .AddCookie("Cookies", options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
            })
            .AddOpenIdConnect("oidc", options =>
            {
                // idp configuration (hybrid flow)
                options.Authority = Configuration["Authentication:Authority"];
                options.ClientId = Configuration["Authentication:ClientId"];
                options.ClientSecret = Configuration["Authentication:ClientSecret"];
                options.ResponseType = "code id_token";

                // token and claim configuration
                options.SaveTokens = true;
                options.Scope.Add("openid");
                options.Scope.Add("email");
                options.Scope.Add("profile");
                options.Scope.Add("role");
                options.Scope.Add("legacy_ids");

                // ensures that the name claim is used to populate ASP.NET Identity username, i.e. User.Identity.Name
                options.TokenValidationParameters.NameClaimType = "name";

                // ensures that any role claims are used to populate ASP.NET Identity roles (centralised entitlement management)
                options.ClaimActions.MapJsonKey("role", "role", "role");
                options.TokenValidationParameters.RoleClaimType = "role";

                // create a user object in our database the first time they login
                // or update them if their claims change
                options.Events.OnTicketReceived = async ctx => { await UserManagement.UpdateUserFromClaimsAsync(ctx); };
            });

        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture("en-GB");
            options.SupportedCultures = new List<CultureInfo> { new("en-GB"), new("en-GB") };
        });

        // the site can be configured to require a staff role to access the entire site.
        // this is useful for launching the site or performing emergency maintenance.
        if (bool.Parse(Configuration["Authentication:CloseSiteToThePublic"]))
        {
            services.AddMvc(config =>
            {
                // only allow authenticated users with an staff role
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .RequireRole(new List<string> { "Administrator", "Photographer" })
                    .Build();

                config.Filters.Add(new AuthorizeFilter(policy));
            });
        }

        // configure the application tier
        Server.Instance.SetConfigurationAsync(Configuration).Wait();

        // add ImageFlow services for dynamic image generation
        ImageResizing.AddImageFlowBlobService(Configuration, services, FileSpec.SpecOriginal, "/dio/");
        ImageResizing.AddImageFlowBlobService(Configuration, services, FileSpec.SpecOriginal, "/diog/");
        ImageResizing.AddImageFlowBlobService(Configuration, services, FileSpec.Spec3840, "/di3840/");
        ImageResizing.AddImageFlowBlobService(Configuration, services, FileSpec.Spec2560, "/di2560/");
        ImageResizing.AddImageFlowBlobService(Configuration, services, FileSpec.Spec1920, "/di1920/");
        ImageResizing.AddImageFlowBlobService(Configuration, services, FileSpec.Spec800, "/di800/");
        ImageResizing.AddImageFlowBlobService(Configuration, services, FileSpec.SpecLowRes, "/dilr/");

        if (bool.Parse(Configuration["ImageFlow:DiskCacheEnabled"]))
        {
            // store processed image files to local storage to use as a cache
            // for development just create a local folder and reference that in configuration.
            ImageResizing.InitialiseImageFlowDiskCache(Configuration);

            var queueSizeLimitInBytes = long.Parse(Configuration["ImageFlow:QueueSizeLimitInBytes"]);
            var cacheSizeLimitInBytes = long.Parse(Configuration["ImageFlow:CacheSizeLimitInBytes"]);

            services.AddImageflowHybridCache(new HybridCacheOptions(Configuration["ImageFlow:DiskCachePath"])
            {
                // How much RAM to use for the write queue before switching to synchronous writes
                QueueSizeLimitInBytes = queueSizeLimitInBytes,

                // The maximum size of the cache on disk
                CacheSizeLimitInBytes = cacheSizeLimitInBytes,
            });
        }

        services.AddHostedService<NotificationService>();
        services.AddApplicationInsightsTelemetry();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // needed when running behind a reverse proxy (NGINX in our case)
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        // configure ImageFlow for image resizing and serving
        // ImageFlow says: it's a good idea to limit image sizes for security. Requests causing these to be exceeded will fail
        // ImageFlow says: the last argument to FrameSizeLimit() is the maximum number of megapixels
        // we don't resize larger images to reduce the number of unique urls so we can increase caching hits, it's a trade off 
        // as we increase filesize, keep the same total download time but cache more images as we are only returning about five
        // possible image sizes rather than millions more permutations.
        // ImageFlow doesn't seem to be able to watermark images without width/height arguments, so use a RewriteHandler to ensure they're always set internally.
        // Security: Image size limits set to 16000x16000 to accommodate professional cameras whilst preventing resource exhaustion attacks
        // - Handles current high-end cameras (e.g., Phase One IQ4 150MP: 14204x10652)
        // - Allows for panoramas and stitched images
        // - Prevents DoS attacks from requesting massive image resizes
        app.UseImageflow(new ImageflowMiddlewareOptions()
            .SetJobSecurityOptions(new SecurityOptions()
                .SetMaxDecodeSize(new FrameSizeLimit(16000, 16000, 200))
                .SetMaxFrameSize(new FrameSizeLimit(16000, 16000, 200))
                .SetMaxEncodeSize(new FrameSizeLimit(16000, 16000, 200)))
            .SetAllowCaching(bool.Parse(Configuration["ImageFlow:ClientCachingEnabled"]))
            .SetDefaultCacheControlString("public, max-age=2592000")
            .MapPath("/local-images", Path.Combine(env.WebRootPath, "img"))
            .AddRewriteHandler("/dio/", ImageResizing.EnsureOriginalImageDimensionsAreLimited)
            .AddRewriteHandler("/di3840/", ImageResizing.EnsureDi3840DimensionsAreSpecified)
            .AddRewriteHandler("/di2560/", ImageResizing.EnsureDi2560DimensionsAreSpecified)
            .AddRewriteHandler("/di1920/", ImageResizing.EnsureDi1920DimensionsAreSpecified)
            .AddRewriteHandler("/diog/", ImageResizing.OpenGraphImageHandler)
            .AddWatermarkingHandler("/dio/", ImageResizing.AddWatermark)
            .AddWatermarkingHandler("/diog/", ImageResizing.AddWatermark)
            .AddWatermarkingHandler("/di3840/", ImageResizing.AddWatermark)
            .AddWatermarkingHandler("/di2560/", ImageResizing.AddWatermark)
            .AddWatermarkingHandler("/di1920/", ImageResizing.AddWatermark));

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSession();
        app.UseRequestLocalization();

        app.UseEndpoints(endpoints => 
        {
            endpoints.MapAreaControllerRoute(
                name: "AdminGalleryImages",
                areaName: "Admin",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{categoryId}/{galleryId}/{imageId}");
            endpoints.MapControllerRoute(
                name: "AdminWithPartitionKey",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{pk}/{id}");
            endpoints.MapControllerRoute(
                name: "Admin",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
            endpoints.MapControllerRoute(
                name: "Category",
                pattern: "c/{name}", new { controller = "Categories", action = "Details" });
            endpoints.MapControllerRoute(
                name: "Gallery",
                pattern: "g/{categoryName}/{galleryId}/{name}", new { controller = "Galleries", action = "Details" });
            endpoints.MapControllerRoute(
                name: "GalleryImage",
                pattern: "i/{galleryId}/{imageId}/{name}", new { controller = "Images", action = "Details" });
            endpoints.MapControllerRoute(
                name: "ImageTag",
                pattern: "t/{tag}", new { controller = "Tags", action = "Details" });
            endpoints.MapControllerRoute(
                name: "Home",
                pattern: "{action}",
                new { action = "Index", controller = "Home" });
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
        });
    }
}