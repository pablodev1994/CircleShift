using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CircleShift.Tests.Api;

public sealed class AuthEndpointTests(AccountsApiFactory factory) : IClassFixture<AccountsApiFactory>
{
    [Fact]
    public async Task Register_signs_in_with_baseline_permissions_and_hardened_cookies()
    {
        using var session = factory.NewSession();
        await session.RefreshCsrfAsync();

        var response = await session.PostAsync("/api/v1/auth/register", new
        {
            email = " New.Member@Example.org ",
            password = ApiSession.Password,
            displayName = "New Member",
            timeZoneId = "America/Bogota",
            confirmedAdult = true,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sessionCookie = response.Headers.GetValues("Set-Cookie").Single(cookie => cookie.StartsWith("__Host-circleshift-session=", StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", sessionCookie, StringComparison.OrdinalIgnoreCase);

        var me = await session.MeAsync();
        Assert.Equal("New Member", me.GetProperty("displayName").GetString());
        Assert.Empty(me.GetProperty("roles").EnumerateArray());
        Assert.Equal(
            ["exchanges.participate", "posts.write", "profile.edit", "wallet.view_own"],
            me.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task Register_without_adult_confirmation_is_refused_with_the_error_envelope()
    {
        using var session = factory.NewSession();
        await session.RefreshCsrfAsync();

        var response = await session.PostAsync("/api/v1/auth/register", new
        {
            email = "minor@example.org",
            password = ApiSession.Password,
            displayName = "Not Confirmed",
            timeZoneId = "Europe/Madrid",
            confirmedAdult = false,
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("member.adult_confirmation_required", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(error.GetProperty("requestId").GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.Client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_in_any_case_conflicts()
    {
        using var first = factory.NewSession();
        await first.RegisterAsync("dupe@example.org");
        using var second = factory.NewSession();
        await second.RefreshCsrfAsync();

        var response = await second.PostAsync("/api/v1/auth/register", new
        {
            email = "DUPE@example.org",
            password = ApiSession.Password,
            displayName = "Copy",
            timeZoneId = "Europe/Madrid",
            confirmedAdult = true,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("account.email_taken", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Missing_body_fields_return_request_invalid_with_details()
    {
        using var session = factory.NewSession();
        await session.RefreshCsrfAsync();

        var response = await session.PostAsync("/api/v1/auth/register", new { email = "x@example.org" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("request.invalid", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("details").TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task Unsafe_requests_without_the_csrf_header_are_refused()
    {
        using var session = factory.NewSession();
        await session.RefreshCsrfAsync();
        session.Client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        var response = await session.Client.PostAsJsonAsync("/api/v1/auth/sign-in", new { email = "a@example.org", password = "whatever-password" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("auth.csrf_invalid", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_answer()
    {
        using var owner = factory.NewSession();
        await owner.RegisterAsync("same-answer@example.org");
        using var session = factory.NewSession();

        var wrongPassword = await session.SignInAsync("same-answer@example.org", "not the right password");
        var unknown = await session.SignInAsync("nobody@example.org");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(
            (await wrongPassword.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString(),
            (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_one()
    {
        using var owner = factory.NewSession();
        await owner.RegisterAsync("locked@example.org");
        using var attacker = factory.NewSession();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.SignInAsync("locked@example.org", $"guess-number-{attempt}")).StatusCode);
        }

        using var realOwner = factory.NewSession();
        Assert.Equal(HttpStatusCode.Unauthorized, (await realOwner.SignInAsync("locked@example.org")).StatusCode);
    }

    [Fact]
    public async Task Sign_in_then_sign_out_ends_the_session()
    {
        using var owner = factory.NewSession();
        await owner.RegisterAsync("in-and-out@example.org");
        using var session = factory.NewSession();

        Assert.Equal(HttpStatusCode.OK, (await session.SignInAsync("IN-AND-OUT@example.org")).StatusCode);
        Assert.Equal("Test Member", (await session.MeAsync()).GetProperty("displayName").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await session.PostAsync("/api/v1/auth/sign-out")).StatusCode);

        var me = await session.Client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal("auth.required", (await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Health_routes_stay_anonymous_while_api_routes_need_a_session()
    {
        using var session = factory.NewSession();

        Assert.Equal(HttpStatusCode.OK, (await session.Client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.Client.GetAsync("/api/v1/members/me/role-grants")).StatusCode);
    }
}

public sealed class AuthRateLimitTests(StrictRateLimitApiFactory factory) : IClassFixture<StrictRateLimitApiFactory>
{
    [Fact]
    public async Task Sign_in_attempts_are_rate_limited_per_client()
    {
        using var session = factory.NewSession();

        await session.SignInAsync("nobody@example.org");
        await session.SignInAsync("nobody@example.org");
        var third = await session.SignInAsync("nobody@example.org");

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("auth.rate_limited", (await third.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}

public sealed class DevelopmentCookieTests(PlainHttpDevelopmentApiFactory factory) : IClassFixture<PlainHttpDevelopmentApiFactory>
{
    [Fact]
    public async Task Plain_http_development_sessions_work_without_the_host_prefix()
    {
        using var session = factory.NewSession();
        await session.RefreshCsrfAsync();

        var response = await session.PostAsync("/api/v1/auth/register", new
        {
            email = "dev@example.org",
            password = ApiSession.Password,
            displayName = "Dev Member",
            timeZoneId = "Europe/Madrid",
            confirmedAdult = true,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sessionCookie = response.Headers.GetValues("Set-Cookie").Single(cookie => cookie.StartsWith("circleshift-session=", StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Dev Member", (await session.MeAsync()).GetProperty("displayName").GetString());
    }
}
