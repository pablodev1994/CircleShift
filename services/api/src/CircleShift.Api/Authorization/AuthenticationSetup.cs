using System.Threading.RateLimiting;
using CircleShift.Api.Errors;
using CircleShift.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace CircleShift.Api.Authorization;

public static class AuthenticationSetup
{
    public const string CsrfCookieName = "XSRF-TOKEN";
    public const string CsrfHeaderName = "X-XSRF-TOKEN";
    public const string AuthRateLimitPolicy = "auth";

    /// <summary>
    /// <c>Auth:SecureCookies</c> (default <c>true</c>) makes every auth cookie
    /// Secure with the <c>__Host-</c> prefix. Development turns it off so the API
    /// works over plain <c>http://localhost:5080</c>; never turn it off elsewhere.
    /// </summary>
    public static bool SecureCookies(IConfiguration configuration) => configuration.GetValue("Auth:SecureCookies", true);

    public static string SessionCookieName(bool secure) => secure ? "__Host-circleshift-session" : "circleshift-session";

    public static IServiceCollection AddCircleShiftAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var secure = SecureCookies(configuration);
        var securePolicy = secure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookieName(secure);
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = securePolicy;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.SlidingExpiration = true;
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = ReloadMemberAsync,
                    OnRedirectToLogin = context =>
                        ApiErrors.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "auth.required", "Sign in to continue."),
                    OnRedirectToAccessDenied = context =>
                        ApiErrors.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "auth.forbidden", "You do not have permission for this action."),
                };
            });

        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddAuthorization(options =>
        {
            // Every endpoint needs a signed-in adult member unless it opts out
            // with [AllowAnonymous]; a forgotten attribute fails closed.
            var signedInAdult = new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(MemberClaims.AdultConfirmedAt)
                .Build();
            options.DefaultPolicy = signedInAdult;
            options.FallbackPolicy = signedInAdult;

            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(permission, policy => policy
                    .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permission)));
            }
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfHeaderName;
            options.Cookie.Name = secure ? "__Host-circleshift-csrf" : "circleshift-csrf";
            options.Cookie.SecurePolicy = securePolicy;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        var permitPerMinute = configuration.GetValue("RateLimits:AuthPermitPerMinute", 10);
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AuthRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitPerMinute, Window = TimeSpan.FromMinutes(1) }));
            options.OnRejected = (context, _) => new ValueTask(ApiErrors.WriteAsync(
                context.HttpContext,
                StatusCodes.Status429TooManyRequests,
                "auth.rate_limited",
                "Too many attempts. Wait a minute and try again."));
        });

        return services;
    }

    /// <summary>
    /// Rebuilds the principal from the database on every request: verified,
    /// unrevoked roles only. A deleted member is signed out.
    /// </summary>
    private static async Task ReloadMemberAsync(CookieValidatePrincipalContext context)
    {
        var memberId = context.Principal?.MemberId();
        var access = memberId is null
            ? null
            : await context.HttpContext.RequestServices.GetRequiredService<MemberAccessService>()
                .FindAsync(memberId.Value, context.HttpContext.RequestAborted);

        if (access is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        context.ReplacePrincipal(MemberClaims.ForRequest(access, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
