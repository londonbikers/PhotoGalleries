using Imageflow.Fluent;
using Imageflow.Server;
using Imageflow.Server.DiskCache;
using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models.Enums;
using LB.PhotoGalleries.Services;
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
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;

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
                options.Events.OnTicketReceived = async ctx => { await UserManagement.UpdateUserFromClaimsAsync(ctx); };
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
                services.AddImageflowDiskCache(new DiskCacheOptions(Configuration["ImageFlow:DiskCachePath"]));
            }

            services.AddHostedService<NotificationService>();
            services.AddApplicationInsightsTelemetry();
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
    }
}
