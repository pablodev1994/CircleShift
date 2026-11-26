using CircleShift.Domain;
using CircleShift.Domain.Accounts;

namespace CircleShift.Tests.Domain;

public sealed class MemberAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Emails_are_trimmed_and_lower_cased() =>
        Assert.Equal("laura@example.org", MemberAccount.NormalizeEmail("  Laura@Example.ORG "));

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("@example.org")]
    [InlineData("laura@")]
    [InlineData("laura@localhost")]
    [InlineData("two@@example.org")]
    [InlineData("la ura@example.org")]
    public void Malformed_emails_are_rejected(string email) =>
        Assert.Equal("account.email_invalid", Assert.Throws<DomainException>(() => MemberAccount.NormalizeEmail(email)).Code);

    [Theory]
    [InlineData(null)]
    [InlineData("eleven-char")]
    public void Short_passwords_are_rejected(string? password) =>
        Assert.Equal("account.password_length", Assert.Throws<DomainException>(() => MemberAccount.RequireAcceptablePassword(password)).Code);

    [Fact]
    public void Five_failures_lock_the_account_for_fifteen_minutes()
    {
        var account = MemberAccount.Create(Guid.NewGuid(), "laura@example.org", "hash", Now);

        for (var attempt = 0; attempt < MemberAccount.MaxFailedSignIns - 1; attempt++)
        {
            account.RecordFailedSignIn(Now);
        }

        Assert.False(account.IsLockedOut(Now));

        account.RecordFailedSignIn(Now);

        Assert.True(account.IsLockedOut(Now.AddMinutes(14)));
        Assert.False(account.IsLockedOut(Now.AddMinutes(15)));
    }

    [Fact]
    public void Successful_sign_in_clears_failures()
    {
        var account = MemberAccount.Create(Guid.NewGuid(), "laura@example.org", "hash", Now);
        account.RecordFailedSignIn(Now);

        account.RecordSuccessfulSignIn(Now.AddMinutes(1));

        Assert.Equal(0, account.FailedSignInCount);
        Assert.Equal(Now.AddMinutes(1), account.LastSignedInAt);
    }
}
