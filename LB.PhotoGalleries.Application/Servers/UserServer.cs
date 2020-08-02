using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using User = LB.PhotoGalleries.Application.Models.User;

namespace LB.PhotoGalleries.Application.Servers
{
    public class UserServer
    {
        #region constructors
        internal UserServer()
        {
        }
        #endregion

        #region public methods
        public async Task CreateOrUpdateUserAsync(User user)
        {
            if (user == null)
                throw new InvalidOperationException("User is null");

            if (!user.IsValid())
                throw new InvalidOperationException("User is not valid. Check that all required properties are set");

            var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
            var partitionKey = new PartitionKey(user.PartitionKey);
            var response = await container.UpsertItemAsync(user, partitionKey);
            var createdItem = response.StatusCode == HttpStatusCode.Created;
            Debug.WriteLine("UserServer.CreateOrUpdateUserAsync: Created user? " + createdItem);
            Debug.WriteLine("UserServer.CreateOrUpdateUserAsync: Request charge: " + response.RequestCharge);
        }

        public async Task<User> GetUserAsync(string userId)
        {
            var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.id = @userId").WithParameter("@userId", userId);
            var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
            var queryResult = container.GetItemQueryIterator<User>(queryDefinition);

            if (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                foreach (var user in resultSet)
                {
                    Debug.WriteLine("UserServer.GetUserAsync: Found a user with id: " + userId);
                    Debug.WriteLine("UserServer.GetUserAsync: Request charge: " + resultSet.RequestCharge);
                    return user;
                }
            }

            throw new InvalidOperationException("No user found with id: " + userId);
        }

        public async Task<List<User>> GetLatestUsersAsync(int maxResults)
        {
            var queryDefinition = new QueryDefinition("SELECT TOP @maxResults * FROM c ORDER BY c.Created DESC").WithParameter("@maxResults", maxResults);
            return await GetUsersByQueryAsync(queryDefinition);
        }

        public async Task<int> GetUserGalleryCountAsync(User user)
        {
            if (user == null)
                throw new InvalidOperationException("User is null");

            const string queryColumnName = "NumOfGalleries";
            var query = new QueryDefinition($"SELECT COUNT(0) AS {queryColumnName} FROM c WHERE c.CreatedByUserId = @userId").WithParameter("@userId", user.Id);
            return await Server.Instance.Galleries.GetGalleriesScalarByQueryAsync(query, queryColumnName);
        }

        public async Task<int> GetUserCommentCountAsync(User user)
        {
            if (user == null)
                throw new InvalidOperationException("User is null");

            const string queryColumnName = "NumOfComments";
            var query = new QueryDefinition($"SELECT COUNT(0) AS {queryColumnName} FROM c WHERE c.Comments.CreatedByUserId = @userId OR c.Images.Comments.CreatedByUserId = @userId").WithParameter("@userId", user.Id);
            return await Server.Instance.Galleries.GetGalleriesScalarByQueryAsync(query, queryColumnName);
        }
        #endregion

        #region private methods
        private static async Task<List<User>> GetUsersByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
            var queryResult = container.GetItemQueryIterator<User>(queryDefinition);
            var users = new List<User>();

            while (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                Debug.WriteLine("UserServer.GetUsersByQueryAsync: Request charge: " + resultSet.RequestCharge);
                users.AddRange(resultSet);
            }

            return users;
        }
        #endregion
    }
}
