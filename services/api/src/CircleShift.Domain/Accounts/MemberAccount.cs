namespace CircleShift.Domain.Accounts;

/// <summary>
/// Sign-in credentials for a member, kept apart from the profile so profile
/// queries never load the email address or password hash.
/// </summary>
public sealed class MemberAccount
{
    public const int EmailMaxLength = 254;
    public const int PasswordMinLength = 12;
    public const int PasswordMaxLength = 128;
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private MemberAccount()
    {
    }

    public Guid MemberId { get; private set; }

    /// <summary>Trimmed and lower-cased; unique across accounts.</summary>
    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public int FailedSignInCount { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset? LastSignedInAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MemberAccount Create(Guid memberId, string email, string passwordHash, DateTimeOffset now)
    {
        if (memberId == Guid.Empty)
        {
            throw new DomainException("member.id_required", "A member id is required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return new MemberAccount
        {
            MemberId = memberId,
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            CreatedAt = now.ToUniversalTime(),
        };
    }

    public static string NormalizeEmail(string? email)
    {
        var value = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (value.Length > EmailMaxLength
            || at <= 0
            || at != value.LastIndexOf('@')
            || at == value.Length - 1
            || !value[(at + 1)..].Contains('.', StringComparison.Ordinal)
            || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new DomainException("account.email_invalid", "Enter a valid email address.");
        }

        return value;
    }

    /// <summary>Checks length only; strength guidance lives in the client.</summary>
    public static void RequireAcceptablePassword(string? password)
    {
        if (password is null || password.Length is < PasswordMinLength or > PasswordMaxLength)
        {
            throw new DomainException(
                "account.password_length",
                $"Passwords must be {PasswordMinLength}-{PasswordMaxLength} characters.");
        }
    }

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil is { } until && until > now;

    public void RecordFailedSignIn(DateTimeOffset now)
    {
        FailedSignInCount++;
        if (FailedSignInCount >= MaxFailedSignIns)
        {
            LockedUntil = now.ToUniversalTime() + LockoutDuration;
            FailedSignInCount = 0;
        }
    }

    public void RecordSuccessfulSignIn(DateTimeOffset now)
    {
        FailedSignInCount = 0;
        LockedUntil = null;
        LastSignedInAt = now.ToUniversalTime();
    }

    public void ReplacePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }
}
