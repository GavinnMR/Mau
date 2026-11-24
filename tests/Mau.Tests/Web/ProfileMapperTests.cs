using System.Globalization;
using Mau.Core.Users;
using Mau.Tests.Support;
using Mau.Web.Mapping;

namespace Mau.Tests.Web;

public class ProfileMapperTests
{
    [Fact]
    public void ToHistoryViewModel_UnspecifiedSearchedAtUtc_TreatsItAsUtc()
    {
        var searchedAt = new DateTime(2026, 11, 25, 2, 0, 0, DateTimeKind.Unspecified);
        var entry = new SearchLogEntry(1, searchedAt, 1, new[] { 1 });

        var model = ProfileMapper.ToHistoryViewModel(
            new[] { entry }, TestCatalog.CreateSnapshot());

        var expected = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(searchedAt, DateTimeKind.Utc), TimeZoneInfo.Local)
            .ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

        Assert.Equal(expected, Assert.Single(model.Items).WhenText);
    }
}
