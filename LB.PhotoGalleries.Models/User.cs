using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace LB.PhotoGalleries.Models;

public class User
{
    #region accessors
    /// <summary>
    /// The unique identifier for the user. Supplied by our Identity Provider
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; }

    /// <summary>
    /// The name of the user, how they're displayed on the site.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The original picture URL for the user.
    /// </summary>
    [DisplayName("Original Picture URL")]
    public string Picture { get; set; }

    /// <summary>
    /// The id of the user's picture file in our storage.
    /// </summary>
    [DisplayName("Picture URL (Hosted)")]
    public string PictureHostedUrl { get; set; }

    /// <summary>
    /// The email address of the user.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Used by CosmosDB to partition container items to improve querying performance.
    /// The value should be the first character of the id.
    /// </summary>
    [DisplayName("Partition Key")]
    public string PartitionKey { get; set; }

    /// <summary>
    /// The date when the user was created on the site.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.Now;

    /// <summary>
    /// If this user registered in the Apollo era then we have a way to link their identity back to the IDP beyond just email addresses which are not immutable.
    /// </summary>
    [DisplayName("Apollo Legacy Id")]
    public string LegacyApolloId { get; set; }

    public CommunicationPreferences CommunicationPreferences { get; } = new();

    #endregion

    #region public methods
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }

    public bool IsValid()
    {
        if (string.IsNullOrEmpty(Id) ||
            string.IsNullOrEmpty(Name) ||
            string.IsNullOrEmpty(Email) ||
            string.IsNullOrEmpty(PartitionKey))
            return false;

        return true;
    }
    #endregion
}

public class CommunicationPreferences
{
    public bool ReceiveCommentNotifications { get; set; }
}