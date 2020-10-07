using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
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

                // create galleries
                // create gallery comments
                // create images
                // create image comments
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

            var categoriesQuery = "SELECT * FROM [dbo].[apollo_gallery_categories]";
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
    }
}
