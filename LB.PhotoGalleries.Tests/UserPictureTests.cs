using LB.PhotoGalleries.Application.Servers;
using LB.PhotoGalleries.Models;

namespace LB.PhotoGalleries.Tests;

/// <summary>
/// When a user's picture from their identity provider gets downloaded as they sign in.
/// </summary>
public class UserPictureTests
{
    private const string PictureUrl = "https://idp.example.com/pictures/1.jpg";
    private const string OtherPictureUrl = "https://idp.example.com/pictures/2.jpg";
    private const string HostedUrl = "https://storage.example.com/user-pictures/1.jpg";

    [Fact]
    public void NeedsPictureDownload_NewUserWithNoPicture_Downloads()
    {
        Assert.True(UserServer.NeedsPictureDownload(new User(), PictureUrl));
    }

    [Fact]
    public void NeedsPictureDownload_PictureRecordedButNeverStored_Downloads()
    {
        // new users used to be created with Picture already set, which stopped their picture ever being downloaded
        Assert.True(UserServer.NeedsPictureDownload(new User { Picture = PictureUrl }, PictureUrl));
    }

    [Fact]
    public void NeedsPictureDownload_SamePictureAlreadyStored_DoesNotDownload()
    {
        Assert.False(UserServer.NeedsPictureDownload(new User { Picture = PictureUrl, PictureHostedUrl = HostedUrl }, PictureUrl.ToUpper()));
    }

    [Fact]
    public void NeedsPictureDownload_PictureChanged_Downloads()
    {
        Assert.True(UserServer.NeedsPictureDownload(new User { Picture = PictureUrl, PictureHostedUrl = HostedUrl }, OtherPictureUrl));
    }

    [Fact]
    public void NeedsPictureDownload_PictureRemovedByAdmin_DoesNotDownloadItAgain()
    {
        Assert.False(UserServer.NeedsPictureDownload(new User { RemovedPictureUrl = PictureUrl }, PictureUrl));
    }

    [Fact]
    public void NeedsPictureDownload_PictureChangedAfterAdminRemovedOne_Downloads()
    {
        Assert.True(UserServer.NeedsPictureDownload(new User { RemovedPictureUrl = PictureUrl }, OtherPictureUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NeedsPictureDownload_NoPictureClaim_DoesNotDownload(string? pictureUrl)
    {
        Assert.False(UserServer.NeedsPictureDownload(new User(), pictureUrl));
    }
}
