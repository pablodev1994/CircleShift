using CircleShift.Api.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CircleShift.Api.Authorization;

/// <summary>
/// Cookie sessions need CSRF protection: every unsafe method must echo the
/// <c>XSRF-TOKEN</c> cookie in the <c>X-XSRF-TOKEN</c> header. Failures get the
/// error envelope so the client can fetch a fresh token and retry.
/// </summary>
public sealed class CsrfValidationFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(ApiErrors.For(
                context.HttpContext,
                "auth.csrf_invalid",
                "The CSRF token is missing or stale. Fetch /api/v1/auth/csrf and retry."));
        }
    }
}
