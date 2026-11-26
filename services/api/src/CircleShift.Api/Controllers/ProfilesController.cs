using System.ComponentModel.DataAnnotations;
using CircleShift.Api.Authorization;
using CircleShift.Api.Errors;
using CircleShift.Domain.Members;
using CircleShift.Infrastructure.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CircleShift.Api.Controllers;

/// <summary>
/// Member profiles, the directory and blocks. Members only ever edit their own
/// profile (<c>members/me</c>); other profiles are read-only and answer 404
/// whenever the caller may not see them, so hidden profiles and blocks do not
/// reveal themselves.
/// </summary>
[ApiController]
[Route("api/v1/members")]
[Produces("application/json")]
public sealed class ProfilesController(ProfileService profiles) : ControllerBase
{
    [HttpGet("me/profile")]
    [ProducesResponseType<OwnProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OwnProfileResponse>> GetOwn(CancellationToken cancellationToken) =>
        await profiles.GetOwnAsync(User.RequireMemberId(), cancellationToken) is { } member
            ? Ok(OwnProfileResponse.From(member))
            : ProfileNotFound();

    [HttpPut("me/profile")]
    [Authorize(Policy = Permissions.ProfileEdit)]
    [ProducesResponseType<OwnProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OwnProfileResponse>> UpdateOwn(ProfileRequest request, CancellationToken cancellationToken)
    {
        if (!ProfileVisibilities.TryParse(request.Visibility, out var visibility))
        {
            return UnprocessableEntity(ApiErrors.For(HttpContext, "profile.visibility_invalid", "Choose hidden, members or public."));
        }

        var member = await profiles.UpdateOwnAsync(
            User.RequireMemberId(),
            new ProfileUpdate(request.DisplayName, request.TimeZoneId, request.Bio, request.Languages, request.AreaLabel, visibility),
            cancellationToken);
        return Ok(OwnProfileResponse.From(member));
    }

    /// <summary>Visible profiles, by display name. Visitors see public profiles only.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<DirectoryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DirectoryResponse>> Search(
        [FromQuery(Name = "q")] string? text,
        [FromQuery] string? language,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var page = await profiles.SearchAsync(User.MemberId(), new DirectoryQuery(text, language, limit, offset), cancellationToken);
        return Ok(new DirectoryResponse([.. page.Items.Select(MemberProfileResponse.From)], page.Total, page.Limit, page.Offset));
    }

    [HttpGet("{memberId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<MemberProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MemberProfileResponse>> Get(Guid memberId, CancellationToken cancellationToken) =>
        await profiles.FindVisibleAsync(User.MemberId(), memberId, cancellationToken) is { } member
            ? Ok(MemberProfileResponse.From(member))
            : ProfileNotFound();

    [HttpGet("me/blocks")]
    public async Task<ActionResult<IReadOnlyList<BlockedMemberResponse>>> ListBlocks(CancellationToken cancellationToken) =>
        Ok((await profiles.ListBlocksAsync(User.RequireMemberId(), cancellationToken))
            .Select(block => new BlockedMemberResponse(block.MemberId, block.DisplayName, block.BlockedAt))
            .ToList());

    /// <summary>Blocks a member. Repeating it is harmless.</summary>
    [HttpPut("me/blocks/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Block(Guid memberId, CancellationToken cancellationToken)
    {
        await profiles.BlockAsync(User.RequireMemberId(), memberId, cancellationToken);
        return NoContent();
    }

    /// <summary>Lifts a block. Repeating it is harmless.</summary>
    [HttpDelete("me/blocks/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unblock(Guid memberId, CancellationToken cancellationToken)
    {
        await profiles.UnblockAsync(User.RequireMemberId(), memberId, cancellationToken);
        return NoContent();
    }

    private NotFoundObjectResult ProfileNotFound() =>
        NotFound(ApiErrors.For(HttpContext, "profile.not_found", "No profile with this id."));
}

public sealed record ProfileRequest(
    [Required] string DisplayName,
    [Required] string TimeZoneId,
    string? Bio,
    IReadOnlyList<string>? Languages,
    string? AreaLabel,
    [Required] string Visibility);

/// <summary>The caller's own profile, including who can see it.</summary>
public sealed record OwnProfileResponse(
    Guid Id,
    string DisplayName,
    string TimeZoneId,
    string? Bio,
    IReadOnlyList<string> Languages,
    string? AreaLabel,
    string Visibility,
    DateTimeOffset UpdatedAt)
{
    public static OwnProfileResponse From(Member member) => new(
        member.Id,
        member.DisplayName,
        member.TimeZoneId,
        member.Bio,
        member.Languages,
        member.AreaLabel,
        member.Visibility.ToName(),
        member.UpdatedAt);
}

/// <summary>What other members (or visitors, for public profiles) see.</summary>
public sealed record MemberProfileResponse(
    Guid Id,
    string DisplayName,
    string TimeZoneId,
    string? Bio,
    IReadOnlyList<string> Languages,
    string? AreaLabel)
{
    public static MemberProfileResponse From(Member member) => new(
        member.Id,
        member.DisplayName,
        member.TimeZoneId,
        member.Bio,
        member.Languages,
        member.AreaLabel);
}

public sealed record DirectoryResponse(IReadOnlyList<MemberProfileResponse> Items, int Total, int Limit, int Offset);

public sealed record BlockedMemberResponse(Guid MemberId, string DisplayName, DateTimeOffset BlockedAt);
