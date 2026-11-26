using System.Text.Json.Serialization;

namespace CircleShift.Api.Errors;

/// <summary>
/// The error envelope every <c>/api/v1</c> failure uses. <see cref="Code"/> is
/// stable for clients; <see cref="RequestId"/> matches the server logs.
/// </summary>
public sealed record ApiError(
    string Code,
    string Message,
    string RequestId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string[]>? Details = null);

public static class ApiErrors
{
    public static ApiError For(HttpContext context, string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, context.TraceIdentifier, details);

    public static Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(For(context, code, message));
    }

    /// <summary>Maps a domain rule code to an HTTP status.</summary>
    public static int StatusFor(string code) => code switch
    {
        _ when code.EndsWith(".not_found", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
        "account.email_taken" or "role.already_open" or "role.already_verified" or "role.revoked" or "role.admin_exists" or "role.last_admin"
            => StatusCodes.Status409Conflict,
        "role.self_verification" => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}
