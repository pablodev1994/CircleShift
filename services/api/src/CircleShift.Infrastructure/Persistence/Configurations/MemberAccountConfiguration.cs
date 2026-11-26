using CircleShift.Domain.Accounts;
using CircleShift.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CircleShift.Infrastructure.Persistence.Configurations;

internal sealed class MemberAccountConfiguration : IEntityTypeConfiguration<MemberAccount>
{
    public void Configure(EntityTypeBuilder<MemberAccount> builder)
    {
        builder.ToTable("member_accounts", table =>
        {
            // The application lower-cases emails; the database refuses anything else
            // so the unique index really means "one account per address".
            table.HasCheckConstraint("ck_member_accounts_email_lower", "email = lower(email)");
            table.HasCheckConstraint("ck_member_accounts_failed_sign_ins", "failed_sign_in_count >= 0");
        });
        builder.HasKey(account => account.MemberId).HasName("pk_member_accounts");
        builder.HasIndex(account => account.Email).IsUnique().HasDatabaseName("ux_member_accounts_email");
        builder.HasOne<Member>()
            .WithOne()
            .HasForeignKey<MemberAccount>(account => account.MemberId)
            .HasConstraintName("fk_member_accounts_members")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(account => account.MemberId).HasColumnName("member_id").ValueGeneratedNever();
        builder.Property(account => account.Email).HasColumnName("email").HasMaxLength(MemberAccount.EmailMaxLength).IsRequired();
        builder.Property(account => account.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
        builder.Property(account => account.FailedSignInCount).HasColumnName("failed_sign_in_count");
        builder.Property(account => account.LockedUntil).HasColumnName("locked_until");
        builder.Property(account => account.LastSignedInAt).HasColumnName("last_signed_in_at");
        builder.Property(account => account.CreatedAt).HasColumnName("created_at");
    }
}
