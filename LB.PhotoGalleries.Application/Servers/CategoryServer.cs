using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Utilities;
using LB.PhotoGalleries.Shared;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Application.Servers;

/// <summary>
/// Provides CRUD functionality for Category objects.
/// </summary>
public class CategoryServer
{
    #region members
    private List<Category> _categories;
    #endregion

    #region accessors
    public List<Category> Categories
    {
        get
        {
            if (_categories == null)
                LoadCategoriesAsync().Wait();

            return _categories;
        }
    }
    #endregion

    #region constructors
    internal CategoryServer()
    {
    }
    #endregion

    #region public methods
    public async Task CreateOrUpdateCategoryAsync(Category category)
    {
        if (category == null)
            throw new InvalidOperationException("Category is null");

        if (string.IsNullOrEmpty(category.PartitionKey))
            category.PartitionKey = Constants.CategoriesPartitionKeyValue;

        if (string.IsNullOrEmpty(category.Id))
            category.Id = Utilities.GenerateId();

        if (!category.IsValid())
            throw new InvalidOperationException("Category is not valid. Check that all required properties are set");

        if (!await IsCategoryNameUniqueAsync(category))
            throw new InvalidOperationException("New category name would not be unique. Please change it and try again.");

        var container = Server.Instance.Database.GetContainer(Constants.CategoriesContainerName);
        var response = await container.UpsertItemAsync(category, new PartitionKey(category.PartitionKey));
        var createdItem = response.StatusCode == HttpStatusCode.Created;
        Log.Debug("CategoryServer.CreateOrUpdateCategoryAsync: Created category? " + createdItem);
        Log.Debug($"CategoryServer.CreateOrUpdateCategoryAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");

        // update the categories cache (remove first if this is an edit scenario)
        if (Categories != null)
        {
            Categories.RemoveAll(c => c.Id == category.Id);
            Categories.Add(category);
        }
    }

    /// <summary>
    /// Categories can't be deleted whilst galleries are referencing them so it's helpful to know how many are.
    /// </summary>
    public async Task<int> GetCategoryGalleryCountAsync(Category category)
    {
        if (category == null)
            throw new InvalidOperationException("Category is null");

        var container = Server.Instance.Database.GetContainer(Constants.GalleriesContainerName);
        var query = new QueryDefinition("SELECT COUNT(0) AS NumOfGalleries FROM c WHERE c.CategoryId = @categoryId").WithParameter("@categoryId", category.Id);
        var result = container.GetItemQueryIterator<object>(query);

        var count = 0;
        if (!result.HasMoreResults) 
            return count;

        var resultSet = await result.ReadNextAsync();
        Log.Debug($"CategoryServer.GetCategoryGalleryCountAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
        foreach (JObject item in resultSet)
            count = (int) item["NumOfGalleries"];

        return count;
    }

    public async Task DeleteCategoryAsync(Category category)
    {
        if (category == null)
            throw new InvalidOperationException("Category is null");

        var galleriesCount = await GetCategoryGalleryCountAsync(category);
        if (galleriesCount > 0)
            throw new InvalidOperationException("Category is not empty of galleries. Cannot delete it.");

        var container = Server.Instance.Database.GetContainer(Constants.CategoriesContainerName);
        var response = await container.DeleteItemAsync<Category>(category.Id, new PartitionKey(category.PartitionKey));
            
        Log.Debug($"CategoryServer.DeleteCategoryAsync: Request charge: {response.RequestCharge}. Elapsed time: {response.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
        Log.Debug("CategoryServer.DeleteCategoryAsync: Status code: " + response.StatusCode);

        // remove the category from the cache
        _categories?.Remove(category);
    }

    public Category GetCategory(string categoryId)
    {
        return Categories.SingleOrDefault(c => c.Id == categoryId);
    }
    #endregion

    #region private methods
    /// <summary>
    /// Retrieves the categories from the database and caches them for the GET accessor to use.
    /// </summary>
    private async Task LoadCategoriesAsync()
    {
        _categories = new List<Category>();

        var queryDefinition = new QueryDefinition("SELECT * FROM c");
        var container = Server.Instance.Database.GetContainer(Constants.CategoriesContainerName);
        var queryResult = container.GetItemQueryIterator<Category>(queryDefinition);

        while (queryResult.HasMoreResults)
        {
            var resultSet = await queryResult.ReadNextAsync();
            Log.Debug($"CategoryServer.LoadCategoriesAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
            foreach (var category in resultSet)
                _categories.Add(category);
        }

        Log.Debug($"CategoryServer.LoadCategoriesAsync: Loaded {_categories.Count} categories from the database.");
    }

    /// <summary>
    /// Checks if a category name would be new, either for a new category or an updated one.
    /// </summary>
    /// <remarks>
    /// If this was a high-transaction system then this would probably make sense to perform at the database level via a trigger.
    /// But this isn't, categories won't be created very often so no need to worry about consistency/performance.
    /// </remarks>
    private static async Task<bool> IsCategoryNameUniqueAsync(Category category)
    {
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Name = @name").WithParameter("@name", category.Name);
        var container = Server.Instance.Database.GetContainer(Constants.CategoriesContainerName);
        var queryResult = container.GetItemQueryIterator<Category>(queryDefinition);
        var isUnique = true;

        if (queryResult.HasMoreResults)
        {
            var resultSet = await queryResult.ReadNextAsync();
            Log.Debug($"CategoryServer.IsCategoryNameUniqueAsync: Request charge: {resultSet.RequestCharge}. Elapsed time: {resultSet.Diagnostics.GetClientElapsedTime().TotalMilliseconds} ms");
            foreach (var dbCategory in resultSet)
            {
                if (dbCategory.Id != category.Id)
                    isUnique = false;
            }
        }

        return isUnique;
    }
    #endregion
}
