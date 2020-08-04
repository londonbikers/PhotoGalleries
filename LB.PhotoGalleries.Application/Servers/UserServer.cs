using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        /// <summary>
        /// Deletes a user from the database and any references to the user, which anonymises any content they've created.
        /// This balances the need for a user's right for their personal data to be deleted (GDPR compliance, etc.) with ensuring the integrity of content.
        /// </summary>
        public async Task DeleteUserAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            // get galleries where the user has been involved in some way (author/comments)
            const string query = "SELECT * FROM c WHERE c.CreatedByUserId = @userId OR c.Comments.CreatedByUserId = @userId OR c.Images.Comments.CreatedByUserId = @userId";
            var queryDefinition = new QueryDefinition(query).WithParameter("@userId", user.Id);
            var galleries = await Server.Instance.Galleries.GetGalleriesByQueryAsync(queryDefinition);

            // go through the galleries and look for user references and remove them then update the gallery
            foreach (var gallery in galleries)
            {
                // remove any references to gallery creations
                if (gallery.CreatedByUserId == user.Id)
                    gallery.CreatedByUserId = Constants.AnonUserId;

                // remove any references to gallery comments
                foreach (var galleryComment in gallery.Comments.Where(q => q.CreatedByUserId == user.Id))
                    galleryComment.CreatedByUserId = Constants.AnonUserId;

                // remove any references to image comments
                foreach (var comment in gallery.Images.Values.SelectMany(image => image.Comments.Where(c => c.CreatedByUserId == user.Id)))
                    comment.CreatedByUserId = Constants.AnonUserId;

                await Server.Instance.Galleries.CreateOrUpdateGalleryAsync(gallery);
            }

            // delete the user
            var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
            var result = await container.DeleteItemAsync<User>(user.Id, new PartitionKey(user.PartitionKey));

            Debug.WriteLine("UserServer:DeleteUserAsync: Status code: " + result.StatusCode);
            Debug.WriteLine("UserServer:DeleteUserAsync: Request charge: " + result.RequestCharge);
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
