using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Shared;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Migrator
{
    internal class Program
    {
        #region members
        private static IConfiguration _configuration;
        private static ILogger _log;
        #endregion

        private static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Migrator...");

            if (args == null || args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No configuration filename argument supplied. Cannot continue.");
                return;
            }

            // setup configuration
            _configuration = new ConfigurationBuilder()
                .AddJsonFile(args[0], optional: false, reloadOnChange: true)
                .Build();

            // setup logging
            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/lb.photogalleries.migrator.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                // initialise Server
                await Server.Instance.SetConfigurationAsync(_configuration);

                // create categories
                await MigrateCategoriesAsync();

                // create users who have commented
                await MigrateUsersAsync();

                // create galleries
                // -- create gallery comments
                // -- create images
                //    -- create image comments
                //await MigrateGalleriesAsync();
            }
            catch (Exception exception)
            {
                _log.Fatal(exception, "LB.PhotoGalleries.Migrator.Program.Main() - Unhandled exception!");
            }
        }

        private static async Task MigrateCategoriesAsync()
        {
            await using var categoriesConnection = new SqlConnection(_configuration["Sql:ConnectionString"]);
            await categoriesConnection.OpenAsync();

            const string categoriesQuery = "SELECT * FROM [dbo].[apollo_gallery_categories]";
            await using var categoriesCommand = new SqlCommand(categoriesQuery, categoriesConnection);

            await using var categoriesReader = await categoriesCommand.ExecuteReaderAsync();
            while (await categoriesReader.ReadAsync())
            {
                var c = new Category
                {
                    Name = (string)categoriesReader["f_name"],
                    Description = (string)categoriesReader["f_description"]
                };

                // has this category already been migrated?
                if (!Server.Instance.Categories.Categories.Any(cat => cat.Name.Equals(c.Name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    await Server.Instance.Categories.CreateOrUpdateCategoryAsync(c);
                    _log.Information("Category created! " + c.Name);
                }
                else
                {
                    _log.Information("Category already migrated: " + c.Name);
                }
            }
        }

        private static async Task MigrateUsersAsync()
        {
            // we need to create user objects for everyone who has commented on a gallery or photo
            await using var userConnection = new SqlConnection(_configuration["Sql:ConnectionString"]);
            await userConnection.OpenAsync();

            var usersQuery = $@"select distinct u.*
	            from apollo_users u
	            inner join comments c on c.AuthorID = u.f_uid
	            where c.OwnerType in (1,2) and Photos{_configuration["EnvironmentName"]}Id is null";
            await using var usersCommand = new SqlCommand(usersQuery, userConnection);
            await using var usersReader = await usersCommand.ExecuteReaderAsync();

            // prepare a connection/command for updating the old user object
            await using var legacyUpdateConnection = new SqlConnection(_configuration["Sql:ConnectionString"]);
            await legacyUpdateConnection.OpenAsync();
            await using var legacyUpdateCommand = new SqlCommand(string.Empty, legacyUpdateConnection);

            while (await usersReader.ReadAsync())
            {
                var u = new User
                {
                    Id = Utilities.GenerateId(),
                    Created = (DateTime)usersReader["f_created"],
                    Email = (string)usersReader["f_email"],
                    LegacyApolloId = usersReader["f_uid"].ToString(),
                    Name = (string)usersReader["f_username"]
                };

                // create the new user object
                await Server.Instance.Users.CreateOrUpdateUserAsync(u);

                // update the old user object with the new user object id (so we know not to try and migrate them again if we re-run)
                legacyUpdateCommand.CommandText = $"UPDATE apollo_users SET Photos{_configuration["EnvironmentName"]}Id = '{u.Id}' WHERE f_uid = '{u.LegacyApolloId}'";
                await legacyUpdateCommand.ExecuteNonQueryAsync();
                _log.Information("User created: " + u.Name);
            }
        }

        private static async Task MigrateGalleriesAsync()
        {
            await using var galleriesConnection = new SqlConnection(_configuration["Sql:ConnectionString"]);
            await galleriesConnection.OpenAsync();

            var galleriesQuery = @$"SELECT 
	            g.*,
	            gc.f_name as [CategoryName]
	            FROM [dbo].[apollo_galleries] g
	            INNER JOIN apollo_gallery_category_gallery_relations gcr ON gcr.GalleryID = g.ID
	            INNER JOIN apollo_gallery_categories gc ON gc.ID = gcr.CategoryID WHERE Photos{_configuration["EnvironmentName"]}Id IS NULL";
            await using var galleriesCommand = new SqlCommand(galleriesQuery, galleriesConnection);

            await using var galleriesReader = await galleriesCommand.ExecuteReaderAsync();
            while (await galleriesReader.ReadAsync())
            {
                // create gallery
                var category = Server.Instance.Categories.Categories.Single(c => c.Name.Equals((string)galleriesReader["CategoryName"], StringComparison.CurrentCultureIgnoreCase));
                var g = new Gallery
                {
                    CategoryId = category.Id,
                    Name = (string)galleriesReader["f_title"],
                    Description = (string)galleriesReader["f_description"],
                    Created = (DateTime)galleriesReader["f_creation_date"],
                    Active = (byte)galleriesReader["f_status"] == 1,
                    LegacyNumId = (long)galleriesReader["ID"],
                    LegacyGuidId = (Guid)galleriesReader["f_uid"]
                };

                // create gallery comments



                // create images
                // create image comments
            }
        }
    }
}
