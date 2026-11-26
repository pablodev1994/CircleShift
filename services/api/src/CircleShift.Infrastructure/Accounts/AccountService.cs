using CircleShift.Application.Abstractions;
using CircleShift.Domain;
using CircleShift.Domain.Accounts;
using CircleShift.Domain.Members;
using CircleShift.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CircleShift.Infrastructure.Accounts;

/// <summary>
/// Registration and password sign-in. Emails and passwords never reach the
/// logs; failures are logged with the member id only, when there is one.
/// </summary>
public sealed class AccountService(
    CircleShiftDbContext db,
    IPasswordHasher<MemberAccount> hasher,
    IClock clock,
    ILogger<AccountService> logger)
{
    // Verifying against this hash when the email is unknown keeps the response
    // time close to a real wrong-password attempt.
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<MemberAccount>().HashPassword(null!, "circleshift-timing-equalizer"));

    public async Task<Member> RegisterAsync(Registration registration, CancellationToken cancellationToken = default)
    {
        var email = MemberAccount.NormalizeEmail(registration.Email);
        MemberAccount.RequireAcceptablePassword(registration.Password);

        var now = clock.UtcNow;
        var member = Member.Register(Guid.CreateVersion7(now), registration.DisplayName, registration.TimeZoneId, registration.ConfirmedAdult, now);

        if (await db.Accounts.AnyAsync(account => account.Email == email, cancellationToken))
        {
            throw EmailTaken();
        }

        var account = MemberAccount.Create(member.Id, email, hasher.HashPassword(null!, registration.Password), now);
        db.Members.Add(member);
        db.Accounts.Add(account);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two registrations for the same address raced past the check above.
            throw EmailTaken();
        }

        logger.LogInformation("Member {MemberId} registered", member.Id);
        return member;
    }

    /// <summary>Returns the member id, or <c>null</c> for any failure (unknown email, wrong password, locked).</summary>
    public async Task<Guid?> SignInAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        string normalized;
        try
        {
            normalized = MemberAccount.NormalizeEmail(email);
        }
        catch (DomainException)
        {
            return null;
        }

        var account = await db.Accounts.SingleOrDefaultAsync(candidate => candidate.Email == normalized, cancellationToken);
        if (account is null)
        {
            hasher.VerifyHashedPassword(null!, DummyHash.Value, password ?? string.Empty);
            return null;
        }

        var now = clock.UtcNow;
        if (account.IsLockedOut(now))
        {
            logger.LogWarning("Sign-in refused for member {MemberId}: account temporarily locked", account.MemberId);
            return null;
        }

        var result = hasher.VerifyHashedPassword(null!, account.PasswordHash, password ?? string.Empty);
        if (result == PasswordVerificationResult.Failed)
        {
            account.RecordFailedSignIn(now);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Failed sign-in for member {MemberId}", account.MemberId);
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.ReplacePasswordHash(hasher.HashPassword(null!, password!));
        }

        account.RecordSuccessfulSignIn(now);
        await db.SaveChangesAsync(cancellationToken);
        return account.MemberId;
    }

    private static DomainException EmailTaken() =>
        new("account.email_taken", "An account with this email already exists.");
}

public sealed record Registration(string Email, string Password, string DisplayName, string TimeZoneId, bool ConfirmedAdult);
