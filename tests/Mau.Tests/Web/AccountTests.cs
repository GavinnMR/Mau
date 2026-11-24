using System.Net;
using Mau.Tests.Support;

namespace Mau.Tests.Web;

public class AccountTests : IClassFixture<MauWebFactory>
{
    private readonly MauWebFactory _factory;

    public AccountTests(MauWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRegister_ReturnsForm()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/Account/Register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"Email\"", html);
        Assert.Contains("name=\"ConfirmPassword\"", html);
    }

    [Fact]
    public async Task GetLogin_ReturnsForm()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"RememberMe\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_Succeeds_RedirectsHomeAndShowsLoggedInNavbar()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var email = AuthHelper.NewEmail();
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Account/Register",
            RegisterFields(email, AuthHelper.Password),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());

        using var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        var html = await home.Content.ReadAsStringAsync();
        Assert.Contains("Log out", html);
        Assert.Contains(email, html);
    }

    [Fact]
    public async Task Register_ShowsWelcomeMessageOnlyOnce()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        using var first = await client.GetAsync("/");
        Assert.Contains("Welcome to Mau!", await first.Content.ReadAsStringAsync());

        using var second = await client.GetAsync("/");
        Assert.DoesNotContain("Welcome to Mau!", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShowsDuplicateMessage()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var email = await AuthHelper.RegisterAsync(client);
        using var other = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(other);

        using var response = await AuthHelper.PostFormAsync(
            other,
            "/Account/Register",
            RegisterFields(email, AuthHelper.Password),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "That email is already registered.",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_PasswordMismatch_ShowsMismatchMessage()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Account/Register",
            RegisterFields(AuthHelper.NewEmail(), AuthHelper.Password, "Different1"),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Passwords do not match.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_PasswordWithoutDigit_ShowsDigitMessage()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Account/Register",
            RegisterFields(AuthHelper.NewEmail(), "abcdefgh"),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("at least one digit", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_InvalidEmail_ShowsEmailMessage()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Account/Register",
            RegisterFields("not-an-email", AuthHelper.Password),
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "Enter a valid email address.",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ShowSameMessage()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var email = await AuthHelper.RegisterAsync(client);
        using var other = AuthHelper.CreateClient(_factory);

        var wrongToken = await AuthHelper.GetCsrfTokenAsync(other);
        using var wrong = await AuthHelper.PostFormAsync(
            other,
            "/Account/Login",
            LoginFields(email, "wrongpass1"),
            wrongToken);
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Contains(
            "Invalid email or password.",
            await wrong.Content.ReadAsStringAsync());

        var unknownToken = await AuthHelper.GetCsrfTokenAsync(other);
        using var unknown = await AuthHelper.PostFormAsync(
            other,
            "/Account/Login",
            LoginFields(AuthHelper.NewEmail(), AuthHelper.Password),
            unknownToken);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Contains(
            "Invalid email or password.",
            await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_CorrectPassword_RedirectsHome()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var email = await AuthHelper.RegisterAsync(client);
        using var other = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(other);

        using var response = await AuthHelper.PostFormAsync(
            other,
            "/Account/Login",
            LoginFields(email, AuthHelper.Password),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_LocalReturnUrl_RedirectsThere()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var email = await AuthHelper.RegisterAsync(client);
        using var other = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(other);

        using var response = await AuthHelper.PostFormAsync(
            other,
            "/Account/Login",
            LoginFields(email, AuthHelper.Password, "/Dish/Details/1"),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Dish/Details/1", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_ExternalReturnUrl_RedirectsHome()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var email = await AuthHelper.RegisterAsync(client);
        using var other = AuthHelper.CreateClient(_factory);
        var token = await AuthHelper.GetCsrfTokenAsync(other);

        using var response = await AuthHelper.PostFormAsync(
            other,
            "/Account/Login",
            LoginFields(email, AuthHelper.Password, "https://evil.example/"),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_FiveFailures_LocksAccount()
    {
        using var client = AuthHelper.CreateClient(_factory);
        var email = await AuthHelper.RegisterAsync(client);
        using var other = AuthHelper.CreateClient(_factory);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var token = await AuthHelper.GetCsrfTokenAsync(other);
            using var failure = await AuthHelper.PostFormAsync(
                other,
                "/Account/Login",
                LoginFields(email, "wrongpass1"),
                token);
            Assert.Equal(HttpStatusCode.OK, failure.StatusCode);
        }

        var finalToken = await AuthHelper.GetCsrfTokenAsync(other);
        using var response = await AuthHelper.PostFormAsync(
            other,
            "/Account/Login",
            LoginFields(email, AuthHelper.Password),
            finalToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Too many failed attempts", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Logout_RedirectsHomeAndShowsGuestNavbar()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);
        var token = await AuthHelper.GetCsrfTokenAsync(client);

        using var response = await AuthHelper.PostFormAsync(
            client,
            "/Account/Logout",
            Array.Empty<KeyValuePair<string, string>>(),
            token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());

        using var home = await client.GetAsync("/");
        var html = await home.Content.ReadAsStringAsync();
        Assert.Contains("Log in", html);
        Assert.DoesNotContain("Log out", html);
    }

    [Fact]
    public async Task Login_WithoutCsrfToken_ReturnsBadRequest()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(LoginFields(AuthHelper.NewEmail(), AuthHelper.Password)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GuestHome_ShowsLoginAndRegisterLinks()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/Account/Login\"", html);
        Assert.Contains("href=\"/Account/Register\"", html);
        Assert.DoesNotContain("/Profile/Pantry", html);
    }

    [Fact]
    public async Task LoggedInHome_ShowsProfileLinksAndLogout()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("/Profile/Pantry", html);
        Assert.Contains("/Profile/Favorites", html);
        Assert.Contains("/Profile/History", html);
        Assert.Contains("Log out", html);
    }

    [Fact]
    public async Task LoggedInUser_OpeningLogin_RedirectsHome()
    {
        using var client = AuthHelper.CreateClient(_factory);
        await AuthHelper.RegisterAsync(client);

        using var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AccessDenied_ReturnsPage()
    {
        using var client = AuthHelper.CreateClient(_factory);

        using var response = await client.GetAsync("/Account/AccessDenied");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Access denied", await response.Content.ReadAsStringAsync());
    }

    private static IEnumerable<KeyValuePair<string, string>> RegisterFields(
        string email, string password, string? confirmPassword = null) =>
        new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("ConfirmPassword", confirmPassword ?? password),
        };

    private static IEnumerable<KeyValuePair<string, string>> LoginFields(
        string email, string password, string? returnUrl = null)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("Email", email),
            new("Password", password),
            new("RememberMe", "false"),
        };

        if (returnUrl is not null)
        {
            fields.Add(new KeyValuePair<string, string>("ReturnUrl", returnUrl));
        }

        return fields;
    }
}
