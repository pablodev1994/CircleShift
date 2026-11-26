using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CircleShift.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CircleShift.Tests.Api;

public sealed class ProfileEndpointTests(AccountsApiFactory factory) : IClassFixture<AccountsApiFactory>
{
    [Fact]
    public async Task New_members_start_hidden_and_save_a_normalized_profile()
    {
        using var member = factory.NewSession();
        var memberId = await member.RegisterAsync(Email("owner"), "Owner Member");

        var initial = await member.Client.GetFromJsonAsync<JsonElement>("/api/v1/members/me/profile");
        Assert.Equal("hidden", initial.GetProperty("visibility").GetString());
        Assert.Empty(initial.GetProperty("languages").EnumerateArray());

        var response = await member.PutAsync("/api/v1/members/me/profile", new
        {
            displayName = "  Owner Renamed ",
            timeZoneId = "America/Bogota",
            bio = "Teaches bread baking.\r\nLearning Portuguese.",
            languages = new[] { "ES", "pt-br", "es" },
            areaLabel = " Bogotá ",
            visibility = "Members",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(memberId, saved.GetProperty("id").GetGuid());
        Assert.Equal("Owner Renamed", saved.GetProperty("displayName").GetString());
        Assert.Equal("America/Bogota", saved.GetProperty("timeZoneId").GetString());
        Assert.Equal("Teaches bread baking.\nLearning Portuguese.", saved.GetProperty("bio").GetString());
        Assert.Equal(["es", "pt-BR"], saved.GetProperty("languages").EnumerateArray().Select(tag => tag.GetString()));
        Assert.Equal("Bogotá", saved.GetProperty("areaLabel").GetString());
        Assert.Equal("members", saved.GetProperty("visibility").GetString());

        // The session's display name follows the profile on the next request.
        Assert.Equal("Owner Renamed", (await member.MeAsync()).GetProperty("displayName").GetString());
    }

    [Theory]
    [InlineData("displayName", "x", "member.display_name_length")]
    [InlineData("timeZoneId", "GMT+2", "time_zone.invalid")]
    [InlineData("bio", "long", "profile.bio_length")]
    [InlineData("languages", "english", "profile.language_invalid")]
    [InlineData("areaLabel", "Line one\nLine two", "profile.area_label_invalid")]
    [InlineData("visibility", "friends", "profile.visibility_invalid")]
    public async Task Invalid_profile_values_return_422_and_change_nothing(string field, string value, string code)
    {
        using var member = factory.NewSession();
        await member.RegisterAsync(Email("invalid"), "Valid Name");

        var body = new Dictionary<string, object?>
        {
            ["displayName"] = "Valid Name",
            ["timeZoneId"] = "Europe/Madrid",
            ["bio"] = null,
            ["languages"] = Array.Empty<string>(),
            ["areaLabel"] = null,
            ["visibility"] = "public",
        };
        body[field] = field switch
        {
            "languages" => new[] { value },
            "bio" => new string('a', 501),
            _ => value,
        };

        var response = await member.PutAsync("/api/v1/members/me/profile", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var unchanged = await member.Client.GetFromJsonAsync<JsonElement>("/api/v1/members/me/profile");
        Assert.Equal("hidden", unchanged.GetProperty("visibility").GetString());
    }

    [Fact]
    public async Task Members_cannot_edit_someone_elses_profile()
    {
        using var owner = factory.NewSession();
        var ownerId = await owner.RegisterAsync(Email("victim"), "Profile Owner");
        await owner.SetProfileAsync("Profile Owner", "public");
        using var intruder = factory.NewSession();
        var intruderId = await intruder.RegisterAsync(Email("intruder"), "Intruder");

        // No route edits another member's profile.
        var direct = await intruder.PutAsync($"/api/v1/members/{ownerId}", new { displayName = "Hacked", visibility = "hidden" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, direct.StatusCode);

        // An id in the body is ignored: "me" is always the signed-in member.
        var smuggled = await intruder.PutAsync("/api/v1/members/me/profile", new
        {
            id = ownerId,
            displayName = "Hacked",
            timeZoneId = "Europe/Madrid",
            languages = Array.Empty<string>(),
            visibility = "hidden",
        });
        Assert.Equal(HttpStatusCode.OK, smuggled.StatusCode);
        Assert.Equal(intruderId, (await smuggled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        var ownerProfile = await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/members/me/profile");
        Assert.Equal("Profile Owner", ownerProfile.GetProperty("displayName").GetString());
        Assert.Equal("public", ownerProfile.GetProperty("visibility").GetString());
    }

    [Fact]
    public async Task Profile_edits_need_a_session_and_the_csrf_header()
    {
        using var visitor = factory.NewSession();
        await visitor.RefreshCsrfAsync();
        var anonymous = await visitor.PutAsync("/api/v1/members/me/profile", new { displayName = "Nobody", timeZoneId = "Europe/Madrid", visibility = "public" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.Client.GetAsync("/api/v1/members/me/blocks")).StatusCode);

        using var member = factory.NewSession();
        await member.RegisterAsync(Email("csrf"), "Csrf Member");
        member.Client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        var forged = await member.Client.PutAsJsonAsync("/api/v1/members/me/profile", new
        {
            displayName = "Forged",
            timeZoneId = "Europe/Madrid",
            visibility = "public",
        });
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal("auth.csrf_invalid", (await forged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Hidden_profiles_answer_like_missing_ones_except_to_their_owner()
    {
        using var hidden = factory.NewSession();
        var hiddenId = await hidden.RegisterAsync(Email("hidden"), $"Hidden {Tag()}");
        using var viewer = factory.NewSession();
        await viewer.RegisterAsync(Email("viewer"), "Viewer");
        using var visitor = factory.NewSession();

        Assert.Equal(HttpStatusCode.OK, (await hidden.Client.GetAsync($"/api/v1/members/{hiddenId}")).StatusCode);

        var asMember = await viewer.Client.GetAsync($"/api/v1/members/{hiddenId}");
        var asVisitor = await visitor.Client.GetAsync($"/api/v1/members/{hiddenId}");
        var missing = await viewer.Client.GetAsync($"/api/v1/members/{Guid.NewGuid()}");

        foreach (var response in new[] { asMember, asVisitor, missing })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("profile.not_found", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }

        Assert.DoesNotContain(hiddenId, await DirectoryIdsAsync(viewer, "Hidden"));
    }

    [Fact]
    public async Task Members_only_profiles_need_a_sign_in_and_public_ones_do_not()
    {
        var tag = Tag();
        using var membersOnly = factory.NewSession();
        var membersOnlyId = await membersOnly.RegisterAsync(Email("members"));
        await membersOnly.SetProfileAsync($"Circle {tag} Members", "members");
        using var open = factory.NewSession();
        var openId = await open.RegisterAsync(Email("public"));
        await open.SetProfileAsync($"Circle {tag} Public", "public");
        using var viewer = factory.NewSession();
        await viewer.RegisterAsync(Email("viewer"));
        using var visitor = factory.NewSession();

        Assert.Equal(HttpStatusCode.OK, (await viewer.Client.GetAsync($"/api/v1/members/{membersOnlyId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.Client.GetAsync($"/api/v1/members/{membersOnlyId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.Client.GetAsync($"/api/v1/members/{openId}")).StatusCode);

        Assert.Equal([membersOnlyId, openId], await DirectoryIdsAsync(viewer, $"Circle {tag}"));
        Assert.Equal([openId], await DirectoryIdsAsync(visitor, $"Circle {tag}"));

        // Other members never see the visibility setting itself.
        var seen = await viewer.Client.GetFromJsonAsync<JsonElement>($"/api/v1/members/{openId}");
        Assert.False(seen.TryGetProperty("visibility", out _));
    }

    [Fact]
    public async Task Blocks_hide_both_members_from_each_other_until_lifted()
    {
        var tag = Tag();
        using var blocker = factory.NewSession();
        var blockerId = await blocker.RegisterAsync(Email("blocker"));
        await blocker.SetProfileAsync($"Block {tag} Blocker", "members");
        using var blocked = factory.NewSession();
        var blockedId = await blocked.RegisterAsync(Email("blocked"));
        await blocked.SetProfileAsync($"Block {tag} Blocked", "public");

        Assert.Equal(HttpStatusCode.NoContent, (await blocker.PutAsync($"/api/v1/members/me/blocks/{blockedId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await blocker.PutAsync($"/api/v1/members/me/blocks/{blockedId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await blocker.Client.GetAsync($"/api/v1/members/{blockedId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await blocked.Client.GetAsync($"/api/v1/members/{blockerId}")).StatusCode);
        Assert.Empty(await DirectoryIdsAsync(blocker, $"Block {tag}"));
        Assert.Empty(await DirectoryIdsAsync(blocked, $"Block {tag}"));

        var list = await blocker.Client.GetFromJsonAsync<JsonElement>("/api/v1/members/me/blocks");
        var entry = Assert.Single(list.EnumerateArray());
        Assert.Equal(blockedId, entry.GetProperty("memberId").GetGuid());
        Assert.Empty((await blocked.Client.GetFromJsonAsync<JsonElement>("/api/v1/members/me/blocks")).EnumerateArray());

        // The blocked member cannot lift someone else's block.
        Assert.Equal(HttpStatusCode.NoContent, (await blocked.DeleteAsync($"/api/v1/members/me/blocks/{blockerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await blocked.Client.GetAsync($"/api/v1/members/{blockerId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await blocker.DeleteAsync($"/api/v1/members/me/blocks/{blockedId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await blocker.Client.GetAsync($"/api/v1/members/{blockedId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await blocked.Client.GetAsync($"/api/v1/members/{blockerId}")).StatusCode);
    }

    [Fact]
    public async Task Blocking_yourself_or_an_unknown_member_is_refused()
    {
        using var member = factory.NewSession();
        var memberId = await member.RegisterAsync(Email("self-block"));

        var self = await member.PutAsync($"/api/v1/members/me/blocks/{memberId}");
        var unknown = await member.PutAsync($"/api/v1/members/me/blocks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);
        Assert.Equal("block.self", (await self.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("member.not_found", (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Directory_filters_treat_wildcards_literally_and_normalize_languages()
    {
        var tag = Tag();
        using var portuguese = factory.NewSession();
        var portugueseId = await portuguese.RegisterAsync(Email("pt"));
        await portuguese.SetProfileAsync($"Dir {tag} Ana", "public", "pt-BR", "en");
        using var spanish = factory.NewSession();
        var spanishId = await spanish.RegisterAsync(Email("es"));
        await spanish.SetProfileAsync($"Dir {tag} Bruno", "public", "es");
        using var viewer = factory.NewSession();
        await viewer.RegisterAsync(Email("dir-viewer"));
        await viewer.SetProfileAsync($"Dir {tag} Viewer", "public", "pt-BR");

        Assert.Equal([portugueseId, spanishId], await DirectoryIdsAsync(viewer, $"dir {tag}"));
        Assert.Empty(await DirectoryIdsAsync(viewer, "%"));
        Assert.Empty(await DirectoryIdsAsync(viewer, $"Dir_{tag}"));
        Assert.Equal([portugueseId], await DirectoryIdsAsync(viewer, $"Dir {tag}", "&language=PT-br"));

        var page = await viewer.Client.GetFromJsonAsync<JsonElement>($"/api/v1/members?q={Uri.EscapeDataString($"Dir {tag}")}&limit=1&offset=1");
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Equal(spanishId, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());

        var badPaging = await viewer.Client.GetAsync("/api/v1/members?limit=0");
        var badLanguage = await viewer.Client.GetAsync("/api/v1/members?language=klingon-language");
        Assert.Equal("directory.paging_invalid", (await badPaging.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badLanguage.StatusCode);
    }

    [Fact]
    public async Task Sessions_without_an_adult_confirmation_fail_the_default_policy()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var policies = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallback = (await policies.GetFallbackPolicyAsync())!;

        var memberId = Guid.NewGuid().ToString();
        var withoutConfirmation = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, memberId)], "Cookies"));
        var confirmed = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, memberId), new Claim(MemberClaims.AdultConfirmedAt, "2026-11-26T10:00:00.0000000+00:00")], "Cookies"));

        Assert.False((await authorization.AuthorizeAsync(withoutConfirmation, null, fallback)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(confirmed, null, fallback)).Succeeded);
    }

    private static string Email(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.org";

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static async Task<IReadOnlyList<Guid>> DirectoryIdsAsync(ApiSession session, string text, string extra = "")
    {
        var page = await session.Client.GetFromJsonAsync<JsonElement>($"/api/v1/members?q={Uri.EscapeDataString(text)}{extra}");
        return [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }
}
