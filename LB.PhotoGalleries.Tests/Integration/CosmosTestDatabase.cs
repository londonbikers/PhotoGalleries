using LB.PhotoGalleries.Application;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace LB.PhotoGalleries.Tests.Integration;

/// <summary>
/// Connects the application tier to a Cosmos DB database for the integration tests. Uses the web project's user secrets
/// (CosmosDB:* and Storage:ConnectionString), which environment variables can override (CosmosDB__Uri, etc).
/// The tests only read data, so they can safely run against the dev database.
/// </summary>
public static class CosmosTestDatabase
{
    // the web project's user secrets id
    private const string UserSecretsId = "6ccfcecc-f478-4f1d-adb6-ea751f6c174d";

    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddUserSecrets(UserSecretsId)
        .AddEnvironmentVariables()
        .Build();

    private static readonly Lazy<Task> Initialisation = new(() => Server.Instance.SetConfigurationAsync(Configuration));

    public static bool IsConfigured =>
        !string.IsNullOrEmpty(Configuration["CosmosDB:Uri"]) &&
        !string.IsNullOrEmpty(Configuration["CosmosDB:PrimaryKey"]) &&
        !string.IsNullOrEmpty(Configuration["CosmosDB:DatabaseName"]) &&
        !string.IsNullOrEmpty(Configuration["Storage:ConnectionString"]);

    /// <summary>
    /// Configures Server.Instance the first time it's called.
    /// </summary>
    public static Task InitialiseAsync() => Initialisation.Value;

    /// <summary>
    /// Runs a query directly against a container, bypassing the application tier, to work out what the tests should expect.
    /// </summary>
    public static async Task<List<T>> QueryAsync<T>(string containerName, string query)
    {
        await InitialiseAsync();
        var iterator = Server.Instance.Database.GetContainer(containerName).GetItemQueryIterator<T>(new QueryDefinition(query));
        var results = new List<T>();
        while (iterator.HasMoreResults)
            results.AddRange(await iterator.ReadNextAsync());
        return results;
    }

    public static Task<List<GalleryRow>> GetGalleriesAsync() =>
        QueryAsync<GalleryRow>("Galleries", "SELECT c.id, c.Name, c.Active, c.Created, c.CreatedByUserId, c.Comments FROM c");

    public static Task<List<ImageRow>> GetImagesAsync() =>
        QueryAsync<ImageRow>("Images", "SELECT c.id, c.Name, c.TagsCsv, c.Created, c.Comments FROM c");

    public static Task<List<UserRow>> GetUsersAsync() =>
        QueryAsync<UserRow>("Users", "SELECT c.id, c.Name, c.Email, c.Created FROM c");
}

/// <summary>
/// Skips the test when no Cosmos DB is configured, so the suite still passes on machines and in CI without one.
/// </summary>
public sealed class CosmosFactAttribute : FactAttribute
{
    public CosmosFactAttribute()
    {
        if (!CosmosTestDatabase.IsConfigured)
            Skip = "No Cosmos DB configured. Set CosmosDB:* and Storage:ConnectionString in the web project's user secrets.";
    }
}

/// <summary>
/// Skips the theory when no Cosmos DB is configured.
/// </summary>
public sealed class CosmosTheoryAttribute : TheoryAttribute
{
    public CosmosTheoryAttribute()
    {
        if (!CosmosTestDatabase.IsConfigured)
            Skip = "No Cosmos DB configured. Set CosmosDB:* and Storage:ConnectionString in the web project's user secrets.";
    }
}

public class CommentRow
{
    public string CreatedByUserId { get; set; } = "";
}

public class GalleryRow
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    public string? Name { get; set; }
    public bool Active { get; set; }
    public DateTime Created { get; set; }
    public string? CreatedByUserId { get; set; }
    public List<CommentRow>? Comments { get; set; }
}

public class ImageRow
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? TagsCsv { get; set; }
    public DateTime Created { get; set; }
    public List<CommentRow>? Comments { get; set; }
}

public class UserRow
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Email { get; set; }
    public DateTime Created { get; set; }
}
