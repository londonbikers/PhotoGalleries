using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
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
        }

        public async Task<User> GetUserAsync(string userId)
        {
            var query = $"SELECT * FROM c WHERE c.Id = '@userId'";
            var queryDefinition = new QueryDefinition(query);
            queryDefinition.WithParameter("@userId", userId);
            var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
            var queryResult = container.GetItemQueryIterator<User>(queryDefinition);

            if (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                foreach (var user in resultSet)
                {
                    Debug.WriteLine("UserServer.GetUserAsync(): Found a user with id: " + userId);
                    return user;
                }
            }

            throw new InvalidOperationException("No user found with id: " + userId);
        }
        #endregion
    }
}
