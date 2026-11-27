using Mau.Core.Catalog;
using Mau.Core.Entities;
using Mau.Core.Users;
using Mau.Tests.Support;

namespace Mau.Tests.Core;

public class PreferenceServiceTests
{
    [Fact]
    public async Task Save_Valid_StoresAndReturnsNoErrors()
    {
        var (service, store) = await CreateAsync();

        var errors = await service.SaveAsync("u1", new UserPreferences(2, CookingMethod.Braised, 60));

        Assert.Empty(errors);
        Assert.Equal(new UserPreferences(2, CookingMethod.Braised, 60), await store.GetAsync("u1"));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(999)]
    public async Task Save_ProteinThatIsNotAProtein_IsRejected(int ingredientId)
    {
        var (service, store) = await CreateAsync();

        var errors = await service.SaveAsync("u1", new UserPreferences(ingredientId, null, null));

        Assert.Contains(errors, error => error.Field == "PreferredProteinId");
        Assert.Equal(0, store.SaveCount);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(601)]
    public async Task Save_MaxTimeOutOfRange_IsRejected(int minutes)
    {
        var (service, _) = await CreateAsync();

        var errors = await service.SaveAsync("u1", new UserPreferences(null, null, minutes));

        Assert.Contains(errors, error => error.Field == "MaxCookTimeMinutes");
    }

    [Fact]
    public async Task Save_UndefinedMethod_IsRejected()
    {
        var (service, _) = await CreateAsync();

        var errors = await service.SaveAsync("u1", new UserPreferences(null, (CookingMethod)99, null));

        Assert.Contains(errors, error => error.Field == "PreferredMethod");
    }

    [Fact]
    public async Task Get_ProteinNoLongerInCatalog_IsDropped()
    {
        var (service, store) = await CreateAsync();
        await store.SaveAsync("u1", new UserPreferences(777, CookingMethod.Soup, null));

        Assert.Equal(new UserPreferences(null, CookingMethod.Soup, null), await service.GetAsync("u1"));
    }

    [Fact]
    public async Task NeverIds_SaveKeepsOnlyKnownDistinctIds_AndGetReturnsThem()
    {
        var (service, _) = await CreateAsync();

        var saved = await service.SaveNeverIdsAsync("u1", new[] { 2, 2, 8, 999 });

        Assert.Equal(2, saved);
        Assert.Equal(new[] { 2, 8 }, (await service.GetNeverIdsAsync("u1")).OrderBy(id => id));
    }

    [Fact]
    public async Task NeverIds_SaveNull_Clears()
    {
        var (service, _) = await CreateAsync();
        await service.SaveNeverIdsAsync("u1", new[] { 2 });

        Assert.Equal(0, await service.SaveNeverIdsAsync("u1", null));
        Assert.Empty(await service.GetNeverIdsAsync("u1"));
    }

    private static async Task<(PreferenceService Service, FakePreferenceStore Store)> CreateAsync()
    {
        var catalog = new CatalogStore(new FakeCatalogLoader(TestCatalog.CreateSnapshot()));
        await catalog.ReloadAsync();
        var store = new FakePreferenceStore();
        return (new PreferenceService(store, new FakeNeverListStore(), catalog), store);
    }
}
