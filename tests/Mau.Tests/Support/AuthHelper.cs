using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mau.Tests.Support;

public static class AuthHelper
{
    public const string Password = "Passw0rd1";
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Adm1nPassw0rd";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static HttpClient CreateClient(MauWebFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    public static Task<string> GetCsrfTokenAsync(HttpClient client) =>
        CsrfHelper.GetTokenAsync(client);

    public static Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string url,
        IEnumerable<KeyValuePair<string, string>> fields,
        string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(fields),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        object? body,
        string token)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }

    public static async Task<string> RegisterAsync(HttpClient client, string? email = null)
    {
        email ??= NewEmail();
        var token = await GetCsrfTokenAsync(client);
        using var response = await PostFormAsync(
            client,
            "/Account/Register",
            new[]
            {
                new KeyValuePair<string, string>("Email", email),
                new KeyValuePair<string, string>("Password", Password),
                new KeyValuePair<string, string>("ConfirmPassword", Password),
            },
            token);

        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Registration failed with status {(int)response.StatusCode}.");
        }

        return email;
    }

    public static Task LoginAsAdminAsync(HttpClient client) =>
        LoginAsync(client, AdminEmail, AdminPassword);

    public static async Task LoginAsync(
        HttpClient client, string email, string password = Password)
    {
        var token = await GetCsrfTokenAsync(client);
        using var response = await PostFormAsync(
            client,
            "/Account/Login",
            new[]
            {
                new KeyValuePair<string, string>("Email", email),
                new KeyValuePair<string, string>("Password", password),
                new KeyValuePair<string, string>("RememberMe", "false"),
            },
            token);

        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Login failed with status {(int)response.StatusCode}.");
        }
    }
}
