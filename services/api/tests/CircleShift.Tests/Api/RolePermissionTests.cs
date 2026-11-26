using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CircleShift.Tests.Api;

public sealed class RolePermissionTests(AccountsApiFactory factory) : IClassFixture<AccountsApiFactory>
{
    [Fact]
    public async Task Requested_role_gives_nothing_until_an_admin_verifies_it_and_stops_when_revoked()
    {
        var (admin, _) = await AdminAsync();
        using var member = factory.NewSession();
        var memberId = await member.RegisterAsync($"coordinator-{Guid.NewGuid():N}@example.org");

        var requested = await member.PostAsync("/api/v1/members/me/role-grants", new { role = "coordinator", reason = "I host the Spanish circle" });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var grant = await requested.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("requested", grant.GetProperty("status").GetString());
        var grantId = grant.GetProperty("id").GetGuid();

        Assert.DoesNotContain("circles.manage", Permissions(await member.MeAsync()));

        var verified = await admin.PostAsync($"/api/v1/admin/role-grants/{grantId}/verify");
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        // Same cookie, no new sign-in: permissions are reloaded on every request.
        var me = await member.MeAsync();
        Assert.Equal(["coordinator"], me.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.Contains("circles.manage", Permissions(me));
        Assert.DoesNotContain("roles.manage", Permissions(me));

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/admin/role-grants/{grantId}/revoke")).StatusCode);

        Assert.DoesNotContain("circles.manage", Permissions(await member.MeAsync()));
        var history = await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/admin/members/{memberId}/role-grants");
        Assert.Equal("revoked", history.EnumerateArray().Single().GetProperty("status").GetString());
    }

    [Fact]
    public async Task Members_without_roles_manage_get_403_on_admin_routes()
    {
        using var member = factory.NewSession();
        var memberId = await member.RegisterAsync($"plain-{Guid.NewGuid():N}@example.org");

        var grant = await member.PostAsync($"/api/v1/admin/members/{memberId}/role-grants", new { role = "admin" });
        var list = await member.Client.GetAsync($"/api/v1/admin/members/{memberId}/role-grants");

        Assert.Equal(HttpStatusCode.Forbidden, grant.StatusCode);
        Assert.Equal("auth.forbidden", (await grant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task A_pending_admin_request_does_not_open_admin_routes()
    {
        using var member = factory.NewSession();
        var memberId = await member.RegisterAsync($"wannabe-{Guid.NewGuid():N}@example.org");

        Assert.Equal(HttpStatusCode.Created, (await member.PostAsync("/api/v1/members/me/role-grants", new { role = "admin" })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync($"/api/v1/admin/members/{memberId}/role-grants")).StatusCode);
        Assert.Empty((await member.MeAsync()).GetProperty("roles").EnumerateArray());
    }

    [Fact]
    public async Task Admins_cannot_verify_their_own_request_or_grant_themselves_roles()
    {
        var (admin, adminId) = await AdminAsync();

        var request = await admin.PostAsync("/api/v1/members/me/role-grants", new { role = "moderator" });
        var grantId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var selfVerify = await admin.PostAsync($"/api/v1/admin/role-grants/{grantId}/verify");
        var selfGrant = await admin.PostAsync($"/api/v1/admin/members/{adminId}/role-grants", new { role = "coordinator" });

        Assert.Equal(HttpStatusCode.Forbidden, selfVerify.StatusCode);
        Assert.Equal("role.self_verification", (await selfVerify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, selfGrant.StatusCode);
        Assert.Equal(["admin"], (await admin.MeAsync()).GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Fact]
    public async Task Admin_grant_is_verified_at_once_and_duplicates_conflict()
    {
        var (admin, _) = await AdminAsync();
        using var member = factory.NewSession();
        var memberId = await member.RegisterAsync($"moderator-{Guid.NewGuid():N}@example.org");

        var granted = await admin.PostAsync($"/api/v1/admin/members/{memberId}/role-grants", new { role = "moderator" });
        var again = await admin.PostAsync($"/api/v1/admin/members/{memberId}/role-grants", new { role = "moderator" });

        Assert.Equal(HttpStatusCode.Created, granted.StatusCode);
        Assert.Equal("verified", (await granted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("moderation.review", Permissions(await member.MeAsync()));
    }

    [Fact]
    public async Task Unknown_roles_and_grants_are_reported()
    {
        var (admin, _) = await AdminAsync();
        using var member = factory.NewSession();
        var memberId = await member.RegisterAsync($"unknown-{Guid.NewGuid():N}@example.org");

        var unknownRole = await admin.PostAsync($"/api/v1/admin/members/{memberId}/role-grants", new { role = "superuser" });
        var unknownGrant = await admin.PostAsync($"/api/v1/admin/role-grants/{Guid.NewGuid()}/verify");
        var unknownMember = await admin.PostAsync($"/api/v1/admin/members/{Guid.NewGuid()}/role-grants", new { role = "moderator" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownRole.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownGrant.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownMember.StatusCode);
    }

    [Fact]
    public async Task The_last_admin_cannot_be_revoked()
    {
        var (admin, adminId) = await AdminAsync();
        var adminGrant = (await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/admin/members/{adminId}/role-grants"))
            .EnumerateArray().Single(grant => grant.GetProperty("role").GetString() == "admin").GetProperty("id").GetGuid();

        var response = await admin.PostAsync($"/api/v1/admin/role-grants/{adminGrant}/revoke");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("role.last_admin", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private static IEnumerable<string?> Permissions(JsonElement me) =>
        me.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString());

    /// <summary>One admin per factory (the bootstrap refuses a second), signed in on a fresh session.</summary>
    private async Task<(ApiSession Session, Guid Id)> AdminAsync()
    {
        await AdminGate.WaitAsync();
        try
        {
            if (adminId is null)
            {
                using var registration = factory.NewSession();
                adminId = await registration.RegisterAsync("admin@example.org", "Admin");
                await factory.MakeFirstAdminAsync(adminId.Value);
            }
        }
        finally
        {
            AdminGate.Release();
        }

        var session = factory.NewSession();
        Assert.Equal(HttpStatusCode.OK, (await session.SignInAsync("admin@example.org")).StatusCode);
        return (session, adminId.Value);
    }

    private static readonly SemaphoreSlim AdminGate = new(1, 1);
    private static Guid? adminId;
}
