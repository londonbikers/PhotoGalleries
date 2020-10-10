using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Shared;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Migrator
{
    internal class Program
    {
        #region members
        private static IConfiguration _configuration;
        private static ILogger _log;
        private static Dictionary<Guid, string> _userIds;
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
                await MigrateGalleriesAsync();
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
            _userIds = new Dictionary<Guid, string>();

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

                // keep track of the old and new user ids as we'll need to use them elsewhere in the migration and don't need to keep hitting the database for it
                _userIds.Add((Guid)usersReader["f_uid"], u.Id);

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
            var maxGalleries = int.Parse(_configuration["MaxGalleriesToMigrate"]);

            // get galleries from SQL
            await using var galleriesConnection = new SqlConnection(_configuration["Sql:ConnectionString"]);
            await galleriesConnection.OpenAsync();
            var galleriesQuery = @$"SELECT TOP {maxGalleries}
	            g.*,
	            gc.f_name as [CategoryName]
	            FROM [dbo].[apollo_galleries] g
	            INNER JOIN apollo_gallery_category_gallery_relations gcr ON gcr.GalleryID = g.ID
	            INNER JOIN apollo_gallery_categories gc ON gc.ID = gcr.CategoryID WHERE Photos{_configuration["EnvironmentName"]}ImagesDone IS NULL";
            await using var galleriesCommand = new SqlCommand(galleriesQuery, galleriesConnection);
            await using var galleriesReader = await galleriesCommand.ExecuteReaderAsync();

            // prepare legacy gallery update SQL
            await using var legacyGalleriesUpdateConnection = new SqlConnection(_configuration["Sql:ConnectionString"]);
            await legacyGalleriesUpdateConnection.OpenAsync();
            await using var legacyGalleriesUpdateCommand = new SqlCommand(string.Empty, legacyGalleriesUpdateConnection);

            while (await galleriesReader.ReadAsync())
            {
                Gallery gallery;
                var galleryName = (string)galleriesReader["CategoryName"];
                var category = Server.Instance.Categories.Categories.Single(c => c.Name.Equals(galleryName, StringComparison.CurrentCultureIgnoreCase));

                // have we already migrated the gallery but not all the images?
                if (galleriesReader[$"Photos{_configuration["EnvironmentName"]}Id"] == DBNull.Value)
                {
                    // build gallery object
                    gallery = new Gallery
                    {
                        Id = Utilities.GenerateId(),
                        CategoryId = category.Id,
                        Name = (string)galleriesReader["f_title"],
                        Description = (string)galleriesReader["f_description"],
                        Created = (DateTime)galleriesReader["f_creation_date"],
                        Active = (byte)galleriesReader["f_status"] == 1,
                        LegacyNumId = (long)galleriesReader["ID"],
                        LegacyGuidId = (Guid)galleriesReader["f_uid"]
                    };

                    // add gallery comments to gallery object
                    await AddGalleryCommentsAsync(gallery);

                    // save the gallery at this point so the id is persisted for image referencing
                    await Server.Instance.Galleries.CreateGalleryAsync(gallery);

                    // update old database with new id so we can come back to them if we stop the migration process halfway through migrating the images
                    legacyGalleriesUpdateCommand.CommandText = $"update apollo_galleries set Photos{_configuration["EnvironmentName"]}Id = '{gallery.Id}' where ID = {galleriesReader["ID"]}";
                    await legacyGalleriesUpdateCommand.ExecuteNonQueryAsync();
                    _log.Information("Created gallery for legacy id: " + gallery.LegacyNumId);
                }
                else
                {
                    // we've already migrated the gallery object itself on a previous run, just retrieve it so we can continue with completing migrating images
                    var newGalleryId = (string)galleriesReader[$"Photos{_configuration["EnvironmentName"]}Id"];
                    gallery = await Server.Instance.Galleries.GetGalleryAsync(category.Id, newGalleryId);
                    _log.Information("Retrieved already created gallery with legacy id: " + gallery.LegacyNumId);
                }

                // create images
                await CreateGalleryImagesAsync(gallery);

                // update legacy gallery as done
                legacyGalleriesUpdateCommand.CommandText = $"update apollo_galleries set Photos{_configuration["EnvironmentName"]}ImagesDone = 1 where ID = {galleriesReader["ID"]}";
                await legacyGalleriesUpdateCommand.ExecuteNonQueryAsync();
                _log.Information("Fully migrated gallery with legacy id: " + gallery.LegacyNumId);
            }
        }

        private static async Task AddGalleryCommentsAsync(Gallery gallery)
        {
            await using var galleryCommentsConnection = new SqlConnection(_configuration["Sql:ConnectionString"]);
            await galleryCommentsConnection.OpenAsync();
            var galleryCommentsQuery = $"SELECT * FROM [dbo].[Comments] where [Status] = 1 and OwnerType = 1 and OwnerID = {gallery.LegacyNumId}";
            await using var galleryCommentsCommand = new SqlCommand(galleryCommentsQuery, galleryCommentsConnection);
            await using var galleryCommentsReader = await galleryCommentsCommand.ExecuteReaderAsync();

            while (await galleryCommentsReader.ReadAsync())
            {
                var userId = await GetUserIdAsync((Guid)galleryCommentsReader["AuthorID"]);
                var comment = new Comment
                {
                    Created = (DateTime) galleryCommentsReader["Created"],
                    Text = (string) galleryCommentsReader["Comment"],
                    CreatedByUserId = userId
                };
                gallery.Comments.Add(comment);
                _log.Information($"Created comment for gallery legacy id {gallery.LegacyNumId} for comment made on {comment.Created}");
            }
        }

        private static async Task<string> GetUserIdAsync(Guid userLegacyId)
        {
            // user is in our cache
            if (_userIds.ContainsKey(userLegacyId))
                return _userIds[userLegacyId];

            // user is not in our cache
            var u = await Server.Instance.Users.GetUserByLegacyIdAsync(userLegacyId);
            _userIds.Add(new Guid(u.LegacyApolloId), u.Id);
            return u.Id;
        }

        private static async Task CreateGalleryImagesAsync(Gallery gallery)
        {
            // get legacy images
            await using var imagesConnection = new SqlConnection(_configuration["Sql:ConnectionString"]);
            await imagesConnection.OpenAsync();
            var galleryImagesQuery = $"SELECT * FROM [dbo].[GalleryImages] WHERE [GalleryID] = {gallery.LegacyNumId} AND Photos{_configuration["EnvironmentName"]}Migrated IS NULL AND (Filename1600 <> '' OR Filename1024 <> '') ORDER BY CreationDate";
            await using var imagesCommand = new SqlCommand(galleryImagesQuery, imagesConnection);
            await using var imagesReader = await imagesCommand.ExecuteReaderAsync();

            // prepare image comments SQL
            await using var secondaryConnection = new SqlConnection(_configuration["Sql:ConnectionString"]);
            await secondaryConnection.OpenAsync();

            // what's the starting point for position? bearing in mind we may already have migrated some photos
            var galleryImages = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            // ReSharper disable once PossibleInvalidOperationException - we've created the images so we know we've already set a position
            var position = galleryImages.Count > 0 ? galleryImages.Max(gi => gi.Position.Value) : 0;

            while (await imagesReader.ReadAsync())
            {
                // does the image file exist? find out now before we do anything else
                var file1600 = imagesReader["Filename1600"] != DBNull.Value ? (string)imagesReader["Filename1600"] : null;
                var file1024 = imagesReader["Filename1024"] != DBNull.Value ? (string)imagesReader["Filename1024"] : null;
                var path = file1600.HasValue() ?
                    Path.Combine(_configuration["OriginalFilesPath"], "1600", file1600) :
                    Path.Combine(_configuration["OriginalFilesPath"], "1024", file1024);

                if (!File.Exists(path))
                {
                    _log.Error($"File doesn't exist! {path}");
                    continue;
                }

                var i = new Image
                {
                    Caption = (string)imagesReader["Comment"],
                    Created = (DateTime)imagesReader["CreationDate"],
                    Name = (string)imagesReader["Name"],
                    LegacyNumId = (long)imagesReader["ID"],
                    Position = position
                };

                if (imagesReader["Credit"] != DBNull.Value && imagesReader["Credit"].ToString().HasValue())
                    i.Credit = (string)imagesReader["Credit"];

                if (imagesReader["UID"] != DBNull.Value)
                    i.LegacyGuidId = (Guid)imagesReader["UID"];

                // create image comments
                await AddImageCommentsAsync(i, secondaryConnection);

                // get the original file stream
                await using (var fs = File.OpenRead(path))
                {
                    // create the image
                    await Server.Instance.Images.CreateImageAsync(gallery.CategoryId, gallery.Id, fs, Path.GetFileName(path), i);
                }

                // mark the image as migrated
                var updateImageCommand = new SqlCommand($"UPDATE GalleryImages SET Photos{_configuration["EnvironmentName"]}Migrated = 1 WHERE ID = {i.LegacyNumId}", secondaryConnection);
                await updateImageCommand.ExecuteNonQueryAsync();

                _log.Information($"Migrated image: {path} in gallery legacy id {gallery.LegacyNumId}");
                position += 1;
            }

            // finalise the gallery
            galleryImages = await Server.Instance.Images.GetGalleryImagesAsync(gallery.Id);
            gallery.ImageCount = galleryImages.Count;
            await Server.Instance.Galleries.UpdateGalleryAsync(gallery);

            // mark the gallery as all images migrated
            var updateGalleryCommand = new SqlCommand($"UPDATE apollo_galleries SET Photos{_configuration["EnvironmentName"]}ImagesDone = 1 WHERE ID = {gallery.LegacyNumId}", secondaryConnection);
            await updateGalleryCommand.ExecuteNonQueryAsync();
            _log.Information("Finalised gallery legacy id " + gallery.LegacyNumId);
        }

        private static async Task AddImageCommentsAsync(Image image, SqlConnection connection)
        {
            var query = $"SELECT * FROM [dbo].[Comments] where [Status] = 1 and OwnerType = 2 and OwnerID = {image.LegacyNumId}";
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var userId = await GetUserIdAsync((Guid)reader["AuthorID"]);
                var comment = new Comment
                {
                    Created = (DateTime) reader["Created"],
                    Text = (string) reader["Comment"],
                    CreatedByUserId = userId
                };
                image.Comments.Add(comment);
                _log.Information($"Created comment for image legacy id {image.LegacyNumId} for comment made on {comment.Created}");
            }
        }
    }
}
