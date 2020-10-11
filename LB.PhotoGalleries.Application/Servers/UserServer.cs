using LB.PhotoGalleries.Models.Utilities;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LB.PhotoGalleries.Shared;
using User = LB.PhotoGalleries.Models.User;

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

            if (string.IsNullOrEmpty(user.PartitionKey))
                user.PartitionKey = GetUserPartitionKeyFromId(user.Id);

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
            var query = "SELECT * FROM c WHERE c.CreatedByUserId = @userId OR c.Comments.CreatedByUserId = @userId";
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

                await Server.Instance.Galleries.UpdateGalleryAsync(gallery);
            }

            // go through any comments the user has left against images and anonymise those
            var images = await Server.Instance.Images.GetImagesUserHasCommentedOnAsync(user.Id);
            foreach (var image in images)
            {
                foreach (var comment in image.Comments.Where(c => c.CreatedByUserId == user.Id))
                    comment.CreatedByUserId = Constants.AnonUserId;

                await Server.Instance.Images.UpdateImageAsync(image);
            }

            // delete the user
            var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
            var result = await container.DeleteItemAsync<User>(user.Id, new PartitionKey(user.PartitionKey));

            Debug.WriteLine("UserServer:DeleteUserAsync: Status code: " + result.StatusCode);
            Debug.WriteLine("UserServer:DeleteUserAsync: Request charge: " + result.RequestCharge);
        }

        public async Task<User> GetUserAsync(string userId)
        {
            if (!userId.HasValue())
                return null;

            var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
            var response = await container.ReadItemAsync<User>(userId, new PartitionKey(GetUserPartitionKeyFromId(userId)));
            Debug.WriteLine($"UserServer:GetUserAsync: Request charge: {response.RequestCharge}");
            return response.Resource;
        }

        public async Task<User> GetUserByLegacyIdAsync(Guid legacyApolloId)
        {
            var queryDefinition = new QueryDefinition("SELECT * FROM u WHERE u.LegacyApolloId = @legacyApolloId").WithParameter("@legacyApolloId", legacyApolloId);
            var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
            var queryResult = container.GetItemQueryIterator<User>(queryDefinition);
            var response = await queryResult.ReadNextAsync();
            return response.FirstOrDefault();
        }

        public async Task<List<User>> GetLatestUsersAsync(int maxResults)
        {
            // limit the results to avoid putting excessive strain on the database and from incurring unnecessary charges
            if (maxResults > 100)
                maxResults = 100;

            var queryDefinition = new QueryDefinition("SELECT TOP @maxResults * FROM c ORDER BY c.Created DESC").WithParameter("@maxResults", maxResults);
            return await GetUsersByQueryAsync(queryDefinition);
        }

        /// <summary>
        /// Performs a search for users with a given search term in their name or email address.
        /// </summary>
        public async Task<List<User>> SearchForUsers(string searchString, int maxResults)
        {
            // limit the results to avoid putting excessive strain on the database and from incurring unnecessary charges
            if (maxResults > 100)
                maxResults = 100;

            var queryDefinition =
                new QueryDefinition("SELECT TOP @maxResults * FROM c WHERE CONTAINS(c.Name, @searchString, true) OR CONTAINS(c.Email, @searchString, true) ORDER BY c.Created DESC")
                    .WithParameter("@searchString", searchString)
                    .WithParameter("@maxResults", maxResults);
                
            return await GetUsersByQueryAsync(queryDefinition);
        }

        public async Task<int> GetUserGalleryCountAsync(User user)
        {
            if (user == null)
                throw new InvalidOperationException("User is null");

            var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.CreatedByUserId = @userId").WithParameter("@userId", user.Id);
            return await Server.Instance.Galleries.GetGalleriesScalarByQueryAsync(query);
        }

        public async Task<int> GetUserCommentCountAsync(User user)
        {
            if (user == null)
                throw new InvalidOperationException("User is null");

            var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.Comments.CreatedByUserId = @userId").WithParameter("@userId", user.Id);
            var galleryComments = await Server.Instance.Galleries.GetGalleriesScalarByQueryAsync(query);
            query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.Comments.CreatedByUserId = @userId").WithParameter("@userId", user.Id);
            var imageComments = await Server.Instance.Images.GetImagesScalarByQueryAsync(query);
            return galleryComments + imageComments;
        }

        public string GetUserPartitionKeyFromId(string userId)
        {
            // find first number in user id, which is a guid
            // this could change in the future to just the first character (alpha-numeric) but for now 10 partitions seems suitable
            // for the number of users we expect.

            foreach (var character in userId)
                if (int.TryParse(character.ToString(), out var number))
                    return number.ToString();

            throw new ArgumentException("Argument value does not seem to be a valid Guid.", nameof(userId));
        }
        #endregion

        #region private methods
        private static async Task<List<User>> GetUsersByQueryAsync(QueryDefinition queryDefinition)
        {
            var container = Server.Instance.Database.GetContainer(Constants.UsersContainerName);
            var queryResult = container.GetItemQueryIterator<User>(queryDefinition);
            var users = new List<User>();
            double charge = 0;

            while (queryResult.HasMoreResults)
            {
                var resultSet = await queryResult.ReadNextAsync();
                charge += resultSet.RequestCharge;
                users.AddRange(resultSet);
            }

            Debug.WriteLine("UserServer.GetUsersByQueryAsync: Query: " + queryDefinition.QueryText);
            Debug.WriteLine("UserServer.GetUsersByQueryAsync: Total request charge: " + charge);

            return users;
        }
        #endregion
    }
}
