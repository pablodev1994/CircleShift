using System.ComponentModel.DataAnnotations;
using CircleShift.Api.Authorization;
using CircleShift.Api.Errors;
using CircleShift.Domain.Members;
using CircleShift.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircleShift.Api.Controllers;

/// <summary>
/// Elevated roles. Members may ask for a role; only admins grant, verify and
/// revoke, and nobody verifies their own. Unverified grants give no permissions.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class RoleGrantsController(RoleGrantService grants) : ControllerBase
{
    [HttpGet("members/me/role-grants")]
    public async Task<ActionResult<IReadOnlyList<RoleGrantResponse>>> ListMine(CancellationToken cancellationToken) =>
        Ok((await grants.ListForMemberAsync(User.RequireMemberId(), cancellationToken)).Select(RoleGrantResponse.From).ToList());

    [HttpPost("members/me/role-grants")]
    [ProducesResponseType<RoleGrantResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RoleGrantResponse>> RequestRole(RoleRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseRole(request.Role, out var role, out var problem))
        {
            return problem;
        }

        var grant = await grants.RequestAsync(User.RequireMemberId(), role, request.Reason, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, RoleGrantResponse.From(grant));
    }

    [HttpGet("admin/members/{memberId:guid}/role-grants")]
    [Authorize(Policy = Permissions.RolesManage)]
    public async Task<ActionResult<IReadOnlyList<RoleGrantResponse>>> ListForMember(Guid memberId, CancellationToken cancellationToken) =>
        Ok((await grants.ListForMemberAsync(memberId, cancellationToken)).Select(RoleGrantResponse.From).ToList());

    [HttpPost("admin/members/{memberId:guid}/role-grants")]
    [Authorize(Policy = Permissions.RolesManage)]
    [ProducesResponseType<RoleGrantResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RoleGrantResponse>> Grant(Guid memberId, RoleRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseRole(request.Role, out var role, out var problem))
        {
            return problem;
        }

        var grant = await grants.GrantAsync(User.RequireMemberId(), memberId, role, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, RoleGrantResponse.From(grant));
    }

    [HttpPost("admin/role-grants/{grantId:guid}/verify")]
    [Authorize(Policy = Permissions.RolesManage)]
    public async Task<ActionResult<RoleGrantResponse>> Verify(Guid grantId, CancellationToken cancellationToken) =>
        await grants.VerifyAsync(User.RequireMemberId(), grantId, cancellationToken) is { } grant
            ? Ok(RoleGrantResponse.From(grant))
            : GrantNotFound();

    [HttpPost("admin/role-grants/{grantId:guid}/revoke")]
    [Authorize(Policy = Permissions.RolesManage)]
    public async Task<ActionResult<RoleGrantResponse>> Revoke(Guid grantId, CancellationToken cancellationToken) =>
        await grants.RevokeAsync(User.RequireMemberId(), grantId, cancellationToken) is { } grant
            ? Ok(RoleGrantResponse.From(grant))
            : GrantNotFound();

    private NotFoundObjectResult GrantNotFound() =>
        NotFound(ApiErrors.For(HttpContext, "role.not_found", "No role grant with this id."));

    private bool TryParseRole(string? value, out CommunityRole role, out ActionResult problem)
    {
        problem = null!;
        if (CommunityRoles.TryParse(value, out role))
        {
            return true;
        }

        problem = UnprocessableEntity(ApiErrors.For(HttpContext, "role.unknown", "Role must be coordinator, moderator or admin."));
        return false;
    }
}

public sealed record RoleRequest([Required] string Role, string? Reason = null);

public sealed record RoleGrantResponse(
    Guid Id,
    Guid MemberId,
    string Role,
    string Status,
    string? Reason,
    DateTimeOffset RequestedAt,
    Guid? VerifiedBy,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? RevokedAt)
{
    public static RoleGrantResponse From(RoleGrant grant) => new(
        grant.Id,
        grant.MemberId,
        grant.Role.ToName(),
        grant.IsRevoked ? "revoked" : grant.IsVerified ? "verified" : "requested",
        grant.Reason,
        grant.RequestedAt,
        grant.VerifiedBy,
        grant.VerifiedAt,
        grant.RevokedAt);
}
