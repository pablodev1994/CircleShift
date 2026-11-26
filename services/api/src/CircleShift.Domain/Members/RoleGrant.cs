namespace CircleShift.Domain.Members;

/// <summary>
/// One elevated role for one member. A grant starts unverified when a member
/// asks for it, and only gives permissions after an admin other than the
/// member verifies it. Revoked grants stay as history.
/// </summary>
public sealed class RoleGrant
{
    public const int ReasonMaxLength = 300;

    private RoleGrant()
    {
    }

    public Guid Id { get; private set; }

    public Guid MemberId { get; private set; }

    public CommunityRole Role { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>The admin who verified it; <c>null</c> with a verified time means a seed or bootstrap command.</summary>
    public Guid? VerifiedBy { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public Guid? RevokedBy { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsVerified => VerifiedAt is not null;

    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Only verified, unrevoked grants turn into permissions.</summary>
    public bool IsEffective => IsVerified && !IsRevoked;

    /// <summary>A member asks for a role; it has no effect until verified.</summary>
    public static RoleGrant Request(Guid id, Guid memberId, CommunityRole role, string? reason, DateTimeOffset now) =>
        new()
        {
            Id = RequireId(id),
            MemberId = RequireId(memberId),
            Role = role,
            Reason = NormalizeReason(reason),
            RequestedAt = now.ToUniversalTime(),
        };

    /// <summary>An admin gives a role directly; the grant is verified by that admin.</summary>
    public static RoleGrant GrantedBy(Guid id, Guid memberId, CommunityRole role, Guid adminId, DateTimeOffset now)
    {
        var grant = Request(id, memberId, role, reason: null, now);
        grant.Verify(adminId, now);
        return grant;
    }

    /// <summary>Seed data and the first-admin bootstrap: verified without a verifying member.</summary>
    public static RoleGrant SystemGranted(Guid id, Guid memberId, CommunityRole role, DateTimeOffset now)
    {
        var grant = Request(id, memberId, role, reason: null, now);
        grant.VerifiedAt = grant.RequestedAt;
        return grant;
    }

    public void Verify(Guid adminId, DateTimeOffset now)
    {
        if (adminId == MemberId)
        {
            throw new DomainException("role.self_verification", "Members cannot verify their own role.");
        }

        if (IsRevoked)
        {
            throw new DomainException("role.revoked", "This role grant was revoked.");
        }

        if (IsVerified)
        {
            throw new DomainException("role.already_verified", "This role grant is already verified.");
        }

        VerifiedBy = RequireId(adminId);
        VerifiedAt = now.ToUniversalTime();
    }

    public void Revoke(Guid adminId, DateTimeOffset now)
    {
        if (IsRevoked)
        {
            throw new DomainException("role.revoked", "This role grant was revoked.");
        }

        RevokedBy = RequireId(adminId);
        RevokedAt = now.ToUniversalTime();
    }

    private static Guid RequireId(Guid id) =>
        id == Guid.Empty ? throw new DomainException("role.id_required", "Role grants need member and grant ids.") : id;

    private static string? NormalizeReason(string? reason)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > ReasonMaxLength || trimmed.Any(char.IsControl))
        {
            throw new DomainException("role.reason_invalid", $"Reasons are single-line text up to {ReasonMaxLength} characters.");
        }

        return trimmed;
    }
}
