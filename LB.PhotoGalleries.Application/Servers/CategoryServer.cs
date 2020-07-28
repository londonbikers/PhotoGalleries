using LB.PhotoGalleries.Application.Models;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.Application.Servers
{
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
                category.Id = Guid.NewGuid().ToString();

            if (!category.IsValid())
                throw new InvalidOperationException("Category is not valid. Check that all required properties are set");

            if (!await IsCategoryNameUniqueAsync(category))
                throw new InvalidOperationException("New category name would not be unique. Please change it and try again.");

            var container = Server.Instance.Database.GetContainer(Constants.CategoriesContainerName);
            var response = await container.UpsertItemAsync(category, new PartitionKey(category.PartitionKey));
            var createdItem = response.StatusCode == HttpStatusCode.Created;
            Debug.WriteLine("CategoryServer.CreateOrUpdateCategoryAsync: Created category? " + createdItem);

            // clear the cached categories list so it's retrieved fresh with these latest changes
            _categories = null;
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
                foreach (var category in resultSet)
                    _categories.Add(category);
            }

            Debug.WriteLine($"CategoryServer.LoadCategoriesAsync(): Loaded ${_categories.Count} categories from the database.");
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
            // need to know:
            // -- is name unique?
            // -- if not unique, are the ids the same, so no name change?
            var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Name = @name").WithParameter("@name", category.Name);
            var container = Server.Instance.Database.GetContainer(Constants.CategoriesContainerName);
            var queryResult = container.GetItemQueryIterator<Category>(queryDefinition);
            var isUnique = true;

            if (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                Debug.WriteLine("CategoryServer.IsCategoryNameUniqueAsync: Request charge: " + resultSet.RequestCharge);
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
}
