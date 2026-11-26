using System.ComponentModel.DataAnnotations;
using CircleShift.Api.Authorization;
using CircleShift.Infrastructure.Accounts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CircleShift.Api.Controllers;

/// <summary>
/// Cookie sessions for the Angular app. Unsafe methods need the
/// <c>X-XSRF-TOKEN</c> header with the value of the <c>XSRF-TOKEN</c> cookie
/// (Angular's HttpClient sends it automatically); call <c>GET csrf</c> first.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(
    AccountService accounts,
    MemberAccessService access,
    IAntiforgery antiforgery,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>Issues the CSRF cookie pair for the current visitor or member.</summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Csrf()
    {
        IssueCsrfToken();
        return NoContent();
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthenticationSetup.AuthRateLimitPolicy)]
    [ProducesResponseType<MeResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MeResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var member = await accounts.RegisterAsync(
            new Registration(request.Email, request.Password, request.DisplayName, request.TimeZoneId, request.ConfirmedAdult),
            cancellationToken);
        var me = await SignInAsync(member.Id, cancellationToken);
        return CreatedAtAction(nameof(Me), me);
    }

    [HttpPost("sign-in")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthenticationSetup.AuthRateLimitPolicy)]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> SignIn(SignInRequest request, CancellationToken cancellationToken)
    {
        var memberId = await accounts.SignInAsync(request.Email, request.Password, cancellationToken);
        if (memberId is null)
        {
            // One answer for unknown email, wrong password and lockout.
            return Unauthorized(Errors.ApiErrors.For(HttpContext, "auth.invalid_credentials", "Email or password is incorrect."));
        }

        return Ok(await SignInAsync(memberId.Value, cancellationToken));
    }

    [HttpPost("sign-out")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOutMember()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.User = new System.Security.Claims.ClaimsPrincipal();
        IssueCsrfToken();
        return NoContent();
    }

    /// <summary>The signed-in member with their verified roles and the permissions those give.</summary>
    [HttpGet("me")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<MeResponse> Me() => Ok(MeResponse.From(User));

    private async Task<MeResponse> SignInAsync(Guid memberId, CancellationToken cancellationToken)
    {
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            MemberClaims.ForCookie(memberId, CookieAuthenticationDefaults.AuthenticationScheme));

        var loaded = await access.FindAsync(memberId, cancellationToken)
            ?? throw new InvalidOperationException($"Member {memberId} disappeared during sign-in.");
        HttpContext.User = MemberClaims.ForRequest(loaded, CookieAuthenticationDefaults.AuthenticationScheme);

        // Antiforgery tokens are tied to the user, so the anonymous token used
        // to sign in is replaced with one for the member.
        IssueCsrfToken();
        return MeResponse.From(HttpContext.User);
    }

    private void IssueCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append(AuthenticationSetup.CsrfCookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = AuthenticationSetup.SecureCookies(configuration) || Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }
}

public sealed record RegisterRequest(
    [Required] string Email,
    [Required] string Password,
    [Required] string DisplayName,
    [Required] string TimeZoneId,
    bool ConfirmedAdult);

public sealed record SignInRequest([Required] string Email, [Required] string Password);

public sealed record MeResponse(Guid Id, string DisplayName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)
{
    public static MeResponse From(System.Security.Claims.ClaimsPrincipal user)
    {
        var roles = user.Roles();
        return new MeResponse(
            user.RequireMemberId(),
            user.Identity?.Name ?? string.Empty,
            [.. roles.Select(role => Domain.Members.CommunityRoles.ToName(role))],
            Authorization.Permissions.For(roles));
    }
}
