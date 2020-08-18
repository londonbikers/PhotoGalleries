using Azure.Storage.Blobs;
using Imageflow.Fluent;
using Imageflow.Server;
using Imageflow.Server.DiskCache;
using Imageflow.Server.Storage.AzureBlob;
using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Application.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;

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
                // idp configuration
                options.Authority = Configuration["Authentication.Authority"];
                options.ClientId = Configuration["Authentication.ClientId"];
                options.ClientSecret = Configuration["Authentication.ClientSecret"];

                // token and claim configuration
                options.GetClaimsFromUserInfoEndpoint = true;
                options.ResponseType = "code";
                options.SaveTokens = true;
                options.Scope.Add("openid");
                options.Scope.Add("email");
                options.Scope.Add("profile");
                options.Scope.Add("role");

                // ensures that the name claim is used to populate ASP.NET Identity username, i.e. User.Identity.Name
                options.TokenValidationParameters.NameClaimType = "name";

                // ensures that role any claims are used to populate ASP.NET Identity roles
                options.ClaimActions.MapJsonKey("role", "role", "role");
                options.TokenValidationParameters.RoleClaimType = "role";

                // create a user object in our database the first time they login
                options.Events.OnTicketReceived = async ctx =>
                {
                    var user = new User
                    {
                        Id = ctx.Principal.FindFirstValue("sub"),
                        Name = ctx.Principal.FindFirstValue("name"),
                        Email = ctx.Principal.FindFirstValue("email"),
                        Picture = ctx.Principal.FindFirstValue("picture")
                    };

                    // we'll either create them or update them, which is useful if their
                    // profile picture has changed from their source identity provider, i.e. Facebook
                    await Server.Instance.Users.CreateOrUpdateUserAsync(user);
                };
            });

            // configure the application tier
            Server.Instance.SetConfigurationAsync(Configuration).Wait();

            // add ImageFlow service for image resizing and serving
            // make the originals container available on /i
            services.AddImageflowAzureBlobService(new AzureBlobServiceOptions(Configuration["Storage:ConnectionString"], new BlobClientOptions())
                    .MapPrefix("/i/", Constants.StorageOriginalContainerName));

            // store processed image files to local storage to use as a cache
            // for development just create a local folder and reference that in configuration.
            // for production we intend on using local Azure App Service storage (d:\local). This is ephemeral but free!
            InitialiseImageFlowDiskCache();
            services.AddImageflowDiskCache(new DiskCacheOptions(Configuration["ImageFlow:DiskCachePath"]));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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
            app.UseImageflow(new ImageflowMiddlewareOptions()
                .SetJobSecurityOptions(new SecurityOptions()
                    .SetMaxDecodeSize(new FrameSizeLimit(9000, 9000, 60))
                    .SetMaxFrameSize(new FrameSizeLimit(9000, 9000, 60))
                    .SetMaxEncodeSize(new FrameSizeLimit(9000, 9000, 30)))
                .SetAllowDiskCaching(true)
                .AddCommandDefault("down.filter", "mitchell"));

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAreaControllerRoute(
                    name: "AdminGalleryImages",
                    areaName: "Admin",
                    pattern: "Admin/{controller=Home}/{action=Index}/{categoryId}/{galleryId}/{imageId}");
                endpoints.MapControllerRoute(
                    name: "AdminWithPartitionKey",
                    pattern: "{area:exists}/{controller=Home}/{action=Index}/{pk}/{id}");
                endpoints.MapControllerRoute(
                    name: "Admin",
                    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        #region private methods
        private void InitialiseImageFlowDiskCache()
        {
            var path = Configuration["ImageFlow:DiskCachePath"];
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
        #endregion
    }
}
