using System.Net.Http.Json;
using System.Text.Json;

namespace CircleShift.Tests.Api;

/// <summary>
/// A browser-like client: keeps cookies and echoes the XSRF-TOKEN cookie in
/// the X-XSRF-TOKEN header, the way Angular's HttpClient does.
/// </summary>
public sealed class ApiSession(HttpClient client) : IDisposable
{
    public const string Password = "correct horse battery";

    public HttpClient Client { get; } = client;

    public async Task RefreshCsrfAsync()
    {
        var response = await Client.GetAsync("/api/v1/auth/csrf");
        response.EnsureSuccessStatusCode();
        TakeCsrfToken(response);
    }

    public async Task<HttpResponseMessage> PostAsync(string path, object? body = null)
    {
        var response = await Client.PostAsJsonAsync(path, body ?? new { });
        TakeCsrfToken(response);
        return response;
    }

    public async Task<Guid> RegisterAsync(string email, string displayName = "Test Member")
    {
        await RefreshCsrfAsync();
        var response = await PostAsync("/api/v1/auth/register", new
        {
            email,
            password = Password,
            displayName,
            timeZoneId = "Europe/Madrid",
            confirmedAdult = true,
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public async Task<HttpResponseMessage> SignInAsync(string email, string password = Password)
    {
        await RefreshCsrfAsync();
        return await PostAsync("/api/v1/auth/sign-in", new { email, password });
    }

    public async Task<JsonElement> MeAsync()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public void Dispose() => Client.Dispose();

    private void TakeCsrfToken(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return;
        }

        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal))
            {
                var value = cookie["XSRF-TOKEN=".Length..].Split(';')[0];
                Client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
                Client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", value);
            }
        }
    }
}
