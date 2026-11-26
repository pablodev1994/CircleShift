using System.Globalization;
using System.Security.Claims;
using CircleShift.Domain.Members;
using CircleShift.Infrastructure.Accounts;

namespace CircleShift.Api.Authorization;

public static class MemberClaims
{
    public const string AdultConfirmedAt = "adult_confirmed_at";

    /// <summary>The session cookie holds only the member id; everything else is reloaded per request.</summary>
    public static ClaimsPrincipal ForCookie(Guid memberId, string scheme) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, memberId.ToString())], scheme));

    /// <summary>The principal a request runs as: verified roles only, straight from the database.</summary>
    public static ClaimsPrincipal ForRequest(MemberAccess access, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, access.MemberId.ToString()),
            new(ClaimTypes.Name, access.DisplayName),
            new(AdultConfirmedAt, access.AdultConfirmedAt.ToString("O", CultureInfo.InvariantCulture)),
        };
        claims.AddRange(access.Roles.Select(role => new Claim(ClaimTypes.Role, role.ToName())));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme, ClaimTypes.Name, ClaimTypes.Role));
    }

    public static Guid? MemberId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static Guid RequireMemberId(this ClaimsPrincipal user) =>
        user.MemberId() ?? throw new InvalidOperationException("No signed-in member on this request.");

    public static IReadOnlyList<CommunityRole> Roles(this ClaimsPrincipal user) =>
        [.. user.FindAll(ClaimTypes.Role)
            .Select(claim => CommunityRoles.TryParse(claim.Value, out var role) ? role : (CommunityRole?)null)
            .OfType<CommunityRole>()
            .Distinct()
            .Order()];
}
