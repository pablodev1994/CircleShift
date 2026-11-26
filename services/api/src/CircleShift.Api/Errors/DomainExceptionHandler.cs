using CircleShift.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace CircleShift.Api.Errors;

/// <summary>Turns broken business rules into the error envelope instead of a 500.</summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domain)
        {
            return false;
        }

        httpContext.Response.StatusCode = ApiErrors.StatusFor(domain.Code);
        await httpContext.Response.WriteAsJsonAsync(ApiErrors.For(httpContext, domain.Code, domain.Message), cancellationToken);
        return true;
    }
}
