using LB.PhotoGalleries.Application;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;

namespace LB.PhotoGalleries.Tests.Integration;

/// <summary>
/// The admin users list: paging, sorting and search.
/// </summary>
public class UserQueryTests
{
    private static async Task<List<User>> GetAllPagesAsync(string? search, UserSortBy sortBy, bool descending)
    {
        var users = new List<User>();
        PagedResultSet<User> page;
        var pageNumber = 1;
        do
        {
            page = await Server.Instance.Users.GetUsersAsync(search, sortBy, descending, pageNumber++, 50);
            users.AddRange(page.Results);
        } while (page.CurrentPage < page.TotalPages);

        return users;
    }

    [CosmosTheory]
    [InlineData(UserSortBy.Name, false)]
    [InlineData(UserSortBy.Name, true)]
    [InlineData(UserSortBy.Email, false)]
    [InlineData(UserSortBy.Email, true)]
    [InlineData(UserSortBy.Created, false)]
    [InlineData(UserSortBy.Created, true)]
    public async Task GetUsersAsync_PagesThroughEveryUserInOrder(UserSortBy sortBy, bool descending)
    {
        var expected = await CosmosTestDatabase.GetUsersAsync();

        var actual = await GetAllPagesAsync(null, sortBy, descending);

        Assert.Equal(expected.Select(u => u.Id).ToHashSet(), actual.Select(u => u.Id).ToHashSet());
        Assert.Equal(actual.Count, actual.Select(u => u.Id).Distinct().Count());

        // cosmos sorts strings ordinally (case-sensitive) and puts missing values first
        Comparison<User> compare = sortBy switch
        {
            UserSortBy.Name => (a, b) => string.CompareOrdinal(a.Name, b.Name),
            UserSortBy.Email => (a, b) => string.CompareOrdinal(a.Email, b.Email),
            _ => (a, b) => a.Created.CompareTo(b.Created)
        };
        for (var i = 1; i < actual.Count; i++)
        {
            var order = compare(actual[i - 1], actual[i]);
            Assert.True(descending ? order >= 0 : order <= 0, $"{sortBy} out of order at position {i}");
        }
    }

    [CosmosTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetUsersAsync_SortsByCommentCount(bool descending)
    {
        var counts = await Server.Instance.Users.GetUserCommentCountsAsync();
        var expected = await CosmosTestDatabase.GetUsersAsync();

        var actual = await GetAllPagesAsync(null, UserSortBy.Comments, descending);

        Assert.Equal(expected.Select(u => u.Id).ToHashSet(), actual.Select(u => u.Id).ToHashSet());
        var actualCounts = actual.Select(u => counts.GetValueOrDefault(u.Id)).ToList();
        Assert.Equal(descending ? actualCounts.OrderDescending() : actualCounts.Order(), actualCounts);
    }

    [CosmosFact]
    public async Task GetUsersAsync_SearchesNameAndEmailIgnoringCase()
    {
        var users = await CosmosTestDatabase.GetUsersAsync();
        var name = users.Select(u => u.Name).First(n => n is { Length: >= 4 } && n.All(char.IsAscii))!;

        // flip the case to check the search ignores it
        var term = new string(name.Substring(1, 3).Select(c => char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c)).ToArray());
        var expected = users
            .Where(u => (u.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (u.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(u => u.Id)
            .ToHashSet();

        var actual = await GetAllPagesAsync(term, UserSortBy.Created, true);

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual.Select(u => u.Id).ToHashSet());
    }

    [CosmosFact]
    public async Task GetUsersAsync_ClampsPageAndPageSize()
    {
        var beyondTheEnd = await Server.Instance.Users.GetUsersAsync(null, UserSortBy.Created, true, page: 100000, pageSize: 10);
        Assert.Equal(beyondTheEnd.TotalPages, beyondTheEnd.CurrentPage);
        Assert.NotEmpty(beyondTheEnd.Results);

        var tooBig = await Server.Instance.Users.GetUsersAsync(null, UserSortBy.Created, true, page: 1, pageSize: 1000);
        Assert.Equal(100, tooBig.PageSize);
        Assert.True(tooBig.Results.Count <= 100);
    }
}
