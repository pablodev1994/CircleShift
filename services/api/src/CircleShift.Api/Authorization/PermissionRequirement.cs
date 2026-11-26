using Microsoft.AspNetCore.Authorization;

namespace CircleShift.Api.Authorization;

/// <summary>The caller is an adult-confirmed member whose verified roles include the permission.</summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(claim => claim.Type == MemberClaims.AdultConfirmedAt)
            && Permissions.For(context.User.Roles()).Contains(requirement.Permission, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
