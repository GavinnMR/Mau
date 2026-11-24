using Mau.Data;
using Mau.Data.Identity;
using Mau.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Mau.Tests.Data;

public class IdentityTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public IdentityTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ServiceScope_ResolvesMauDbContextAndUserManager()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<MauDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetService<UserManager<ApplicationUser>>());
    }

    [Fact]
    public async Task CreateAsync_UniqueEmail_SucceedsAndCanBeFoundByEmail()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = NewEmail();
        var user = new ApplicationUser { UserName = email, Email = email };

        var result = await users.CreateAsync(user, "abcdefg1");

        Assert.True(result.Succeeded, Describe(result));
        var found = await users.FindByEmailAsync(email);
        Assert.NotNull(found);
        Assert.Equal(email, found!.UserName);
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail_FailsWithDuplicateCode()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = NewEmail();

        var first = await users.CreateAsync(
            new ApplicationUser { UserName = email, Email = email }, "abcdefg1");
        Assert.True(first.Succeeded, Describe(first));

        var second = await users.CreateAsync(
            new ApplicationUser { UserName = email, Email = email }, "abcdefg1");

        Assert.False(second.Succeeded);
        Assert.Contains(second.Errors, error =>
            error.Code is "DuplicateUserName" or "DuplicateEmail");
    }

    [Theory]
    [InlineData("abc1", "PasswordTooShort")]
    [InlineData("abcdefgh", "PasswordRequiresDigit")]
    [InlineData("ABCDEFG1", "PasswordRequiresLower")]
    public async Task CreateAsync_WeakPassword_FailsWithExpectedCode(
        string password, string expectedCode)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = NewEmail();

        var result = await users.CreateAsync(
            new ApplicationUser { UserName = email, Email = email }, password);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Code));
}
