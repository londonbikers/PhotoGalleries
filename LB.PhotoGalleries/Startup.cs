using Azure.Storage.Blobs;
using Imageflow.Fluent;
using Imageflow.Server;
using Imageflow.Server.DiskCache;
using Imageflow.Server.Storage.AzureBlob;
using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Services;
using LB.PhotoGalleries.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LB.PhotoGalleries
{
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
                options.Cookie.IsEssential = true;
            });

            // make sure all our urls are generated in lower-case for purely aesthetic reasons
            services.Configure<RouteOptions>(o => o.LowercaseUrls = true);

            services.AddControllersWithViews();

            // reduces down the claims received
            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = "Cookies";
                options.DefaultChallengeScheme = "oidc";
            })
            .AddCookie("Cookies")
            .AddOpenIdConnect("oidc", options =>
            {
                // idp configuration (implicit grant type)
                options.Authority = Configuration["Authentication:Authority"];
                options.ClientId = Configuration["Authentication:ClientId"];
                options.ClientSecret = Configuration["Authentication:ClientSecret"];

                // token and claim configuration
                options.SaveTokens = true;
                options.Scope.Add("openid");
                options.Scope.Add("email");
                options.Scope.Add("profile");
                options.Scope.Add("role");
                options.Scope.Add("legacy_ids");

                // ensures that the name claim is used to populate ASP.NET Identity username, i.e. User.Identity.Name
                options.TokenValidationParameters.NameClaimType = "name";

                // ensures that role any claims are used to populate ASP.NET Identity roles
                options.ClaimActions.MapJsonKey("role", "role", "role");
                options.TokenValidationParameters.RoleClaimType = "role";

                // create a user object in our database the first time they login
                // or update them if claims/attributes change
                options.Events.OnTicketReceived = async ctx => { await UpdateUserFromClaimsAsync(ctx); };
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
            AddImageFlowBlobService(services, FileSpec.SpecOriginal, "/dio/");
            AddImageFlowBlobService(services, FileSpec.SpecOriginal, "/diog/");
            AddImageFlowBlobService(services, FileSpec.Spec3840, "/di3840/");
            AddImageFlowBlobService(services, FileSpec.Spec2560, "/di2560/");
            AddImageFlowBlobService(services, FileSpec.Spec1920, "/di1920/");
            AddImageFlowBlobService(services, FileSpec.Spec800, "/di800/");
            AddImageFlowBlobService(services, FileSpec.SpecLowRes, "/dilr/");

            if (bool.Parse(Configuration["ImageFlow:DiskCacheEnabled"]))
            {
                // store processed image files to local storage to use as a cache
                // for development just create a local folder and reference that in configuration.
                InitialiseImageFlowDiskCache();
                services.AddImageflowDiskCache(new DiskCacheOptions(Configuration["ImageFlow:DiskCachePath"]));
            }

            services.AddHostedService<NotificationService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
            app.UseImageflow(new ImageflowMiddlewareOptions()
                .SetJobSecurityOptions(new SecurityOptions()
                    .SetMaxDecodeSize(new FrameSizeLimit(99999, 99999, 100))
                    .SetMaxFrameSize(new FrameSizeLimit(99999, 99999, 100))
                    .SetMaxEncodeSize(new FrameSizeLimit(99999, 99999, 100)))
                .SetAllowDiskCaching(bool.Parse(Configuration["ImageFlow:ClientCachingEnabled"]))
                .MapPath("/local-images", Path.Combine(env.WebRootPath, "img"))
                .AddRewriteHandler("/dio/", EnsureOriginalImageDimensionsAreLimited)
                .AddRewriteHandler("/di3840/", EnsureDi3840DimensionsAreSpecified)
                .AddRewriteHandler("/di2560/", EnsureDi2560DimensionsAreSpecified)
                .AddRewriteHandler("/di1920/", EnsureDi1920DimensionsAreSpecified)
                .AddRewriteHandler("/diog/", OpenGraphImageHandler)
                .AddWatermarkingHandler("/dio/", AddWatermark)
                .AddWatermarkingHandler("/diog/", AddWatermark)
                .AddWatermarkingHandler("/di3840/", AddWatermark)
                .AddWatermarkingHandler("/di2560/", AddWatermark)
                .AddWatermarkingHandler("/di1920/", AddWatermark));

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();

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
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        #region private methods
        private void InitialiseImageFlowDiskCache()
        {
            var path = Configuration["ImageFlow:DiskCachePath"];
            Debug.WriteLine("InitialiseImageFlowDiskCache: path: " + path);
            if (Directory.Exists(path))
                return;

            Debug.WriteLine("InitialiseImageFlowDiskCache: creating new path");
            Directory.CreateDirectory(path);
            Debug.WriteLine("InitialiseImageFlowDiskCache: created new path");
        }

        private void AddImageFlowBlobService(IServiceCollection services, FileSpec fileSpec, string path)
        {
            var imageFlowSpec = ImageFileSpecs.GetImageFileSpec(fileSpec);
            services.AddImageflowAzureBlobService(new AzureBlobServiceOptions(Configuration["Storage:ConnectionString"], new BlobClientOptions())
                .MapPrefix(path, imageFlowSpec.ContainerName));
        }

        private static void AddWatermark(WatermarkingEventArgs args)
        {
            // PROBLEM: we don't know when an image is portrait when default incoming dims are equal!
            // need original dims, don't want to trust client

            var modeSpecified = args.Query.ContainsKey("mode");
            var size = new Size();
            if (args.Query.ContainsKey("w") && int.TryParse(args.Query["w"], out var wParam))
                size.Width = wParam;
            else if (args.Query.ContainsKey("width") && int.TryParse(args.Query["width"], out var widthParam))
                size.Width = widthParam;
            if (args.Query.ContainsKey("h") && int.TryParse(args.Query["h"], out var hParam))
                size.Height = hParam;
            else if (args.Query.ContainsKey("height") && int.TryParse(args.Query["height"], out var heightParam))
                size.Height = heightParam;

            var imageSizeRequiresWatermark = size.Width < 1 || size.Height < 1 || (size.Width > 1000 || size.Height > 1000);
            var referer = args.Context.Request.GetTypedHeaders().Referer;
            var isLocalReferer = referer != null && referer.Host.Equals(args.Context.Request.Host.Host, StringComparison.CurrentCultureIgnoreCase);
            if (modeSpecified && isLocalReferer && !imageSizeRequiresWatermark)
                return;

            // the watermark needs to be a bit bigger when displayed on portrait format images
            var watermarkSizeAsPercent = 13;
            if ((args.Query.ContainsKey("o") && args.Query["o"] == "p") || size.Height > size.Width)
                watermarkSizeAsPercent = 25;

            args.AppliedWatermarks.Add(
                new NamedWatermark("lb-corner-logo", "/local-images/lb-white-stroked-10.png",
                new WatermarkOptions()
                    .SetFitBoxLayout(new WatermarkFitBox(WatermarkAlign.Image, 1, 10, watermarkSizeAsPercent, 99), WatermarkConstraintMode.Within, new ConstraintGravity(0, 100))));
        }

        /// <summary>
        /// Original images should only be requested by legacy clients and should be size-constrained so the endpoint can't be used to download the entire original image
        /// </summary>
        private static void EnsureOriginalImageDimensionsAreLimited(UrlEventArgs args)
        {
            const int maxSize = 3840;
            var width = maxSize;
            var height = maxSize;

            if (args.Query.ContainsKey("w"))
                width = int.Parse(args.Query["w"]);
            else if (args.Query.ContainsKey("width"))
                width = int.Parse(args.Query["w"]);

            if (args.Query.ContainsKey("h"))
                height = int.Parse(args.Query["h"]);
            else if (args.Query.ContainsKey("height"))
                height = int.Parse(args.Query["h"]);

            if (width > maxSize)
                width = maxSize;

            if (height > maxSize)
                height = maxSize;

            args.Query["w"] = width.ToString();
            args.Query["h"] = height.ToString();
        }

        private static void EnsureDi3840DimensionsAreSpecified(UrlEventArgs args)
        {
            var containsWidthParam = args.Query.ContainsKey("w") || args.Query.ContainsKey("width");
            var containsHeightParam = args.Query.ContainsKey("h") || args.Query.ContainsKey("height");
            if (containsWidthParam || containsHeightParam)
                return;

            args.Query["w"] = 3840.ToString();
            args.Query["h"] = 3840.ToString();
        }

        private static void EnsureDi2560DimensionsAreSpecified(UrlEventArgs args)
        {
            var containsWidthParam = args.Query.ContainsKey("w") || args.Query.ContainsKey("width");
            var containsHeightParam = args.Query.ContainsKey("h") || args.Query.ContainsKey("height");
            if (containsWidthParam || containsHeightParam)
                return;

            args.Query["w"] = 2560.ToString();
            args.Query["h"] = 2560.ToString();
        }

        private static void EnsureDi1920DimensionsAreSpecified(UrlEventArgs args)
        {
            var containsWidthParam = args.Query.ContainsKey("w") || args.Query.ContainsKey("width");
            var containsHeightParam = args.Query.ContainsKey("h") || args.Query.ContainsKey("height");
            if (containsWidthParam || containsHeightParam)
                return;

            args.Query["w"] = 1920.ToString();
            args.Query["h"] = 1920.ToString();
        }

        private static void OpenGraphImageHandler(UrlEventArgs args)
        {
            args.Query["w"] = 1080.ToString();
            args.Query["h"] = 1080.ToString();
            args.Query["mode"] = "max";
        }

        private static async Task UpdateUserFromClaimsAsync(TicketReceivedContext ctx)
        {
            var userId = ctx.Principal.FindFirstValue("sub");
            var user = await Server.Instance.Users.GetUserAsync(userId);
            var updateNeeded = false;

            if (user == null)
            {
                // the user is new, create them
                user = new User
                {
                    Id = ctx.Principal.FindFirstValue("sub"),
                    Name = ctx.Principal.FindFirstValue("name"),
                    Email = ctx.Principal.FindFirstValue("email"),
                    Picture = ctx.Principal.FindFirstValue("picture"),
                    LegacyApolloId = ctx.Principal.FindFirstValue("urn:londonbikers:legacyapolloid")
                };

                // set any defaults
                user.CommunicationPreferences.ReceiveCommentNotifications = true;

                updateNeeded = true;
            }
            else
            {
                // we already have an existing user for them, update their attributes if necessary
                if (!user.Name.Equals(ctx.Principal.FindFirstValue("name"), StringComparison.CurrentCultureIgnoreCase))
                {
                    user.Name = ctx.Principal.FindFirstValue("name");
                    updateNeeded = true;
                }

                if (!user.Email.Equals(ctx.Principal.FindFirstValue("email"), StringComparison.CurrentCultureIgnoreCase))
                {
                    user.Email = ctx.Principal.FindFirstValue("email");
                    updateNeeded = true;
                }

                var pictureClaimValue = ctx.Principal.FindFirstValue("picture");
                if (pictureClaimValue.HasValue())
                {
                    // only update the picture if we have an inbound claim
                    if (!user.Picture.HasValue() || !user.Picture.Equals(pictureClaimValue, StringComparison.CurrentCultureIgnoreCase))
                    {
                        // only update the picture if this is the first time we've got a picture or if the picture is different to the one we've already downloaded
                        await Server.Instance.Users.DownloadAndStoreUserPictureAsync(user, pictureClaimValue);
                        updateNeeded = true;
                    }
                }
            }

            if (updateNeeded)
            {
                // we'll either create them or update them, which is useful if their
                // profile picture has changed from their source identity provider, i.e. Facebook
                await Server.Instance.Users.CreateOrUpdateUserAsync(user);
            }
        }
        #endregion
    }
}
