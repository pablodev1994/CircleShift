using CircleShift.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CircleShift.Infrastructure.Persistence.Configurations;

internal sealed class RoleGrantConfiguration : IEntityTypeConfiguration<RoleGrant>
{
    public void Configure(EntityTypeBuilder<RoleGrant> builder)
    {
        builder.ToTable("role_grants", table =>
        {
            table.HasCheckConstraint("ck_role_grants_role", "role IN ('coordinator', 'moderator', 'admin')");
            // Nobody verifies their own role, and a verifier always comes with a time.
            table.HasCheckConstraint("ck_role_grants_not_self_verified", "verified_by IS NULL OR verified_by <> member_id");
            table.HasCheckConstraint("ck_role_grants_verified_pair", "verified_by IS NULL OR verified_at IS NOT NULL");
            table.HasCheckConstraint("ck_role_grants_revoked_pair", "(revoked_by IS NULL OR revoked_at IS NOT NULL)");
        });
        builder.HasKey(grant => grant.Id).HasName("pk_role_grants");

        // At most one open (requested or verified, not revoked) grant per member and role.
        builder.HasIndex(grant => new { grant.MemberId, grant.Role })
            .IsUnique()
            .HasFilter("revoked_at IS NULL")
            .HasDatabaseName("ux_role_grants_open_member_role");

        builder.HasIndex(grant => grant.VerifiedBy).HasDatabaseName("ix_role_grants_verified_by");
        builder.HasIndex(grant => grant.RevokedBy).HasDatabaseName("ix_role_grants_revoked_by");

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(grant => grant.MemberId)
            .HasConstraintName("fk_role_grants_members")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(grant => grant.VerifiedBy)
            .HasConstraintName("fk_role_grants_verified_by")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(grant => grant.RevokedBy)
            .HasConstraintName("fk_role_grants_revoked_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(grant => grant.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(grant => grant.MemberId).HasColumnName("member_id");
        builder.Property(grant => grant.Role)
            .HasColumnName("role")
            .HasConversion(role => role.ToName(), name => ParseRole(name))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(grant => grant.Reason).HasColumnName("reason").HasMaxLength(RoleGrant.ReasonMaxLength);
        builder.Property(grant => grant.RequestedAt).HasColumnName("requested_at");
        builder.Property(grant => grant.VerifiedBy).HasColumnName("verified_by");
        builder.Property(grant => grant.VerifiedAt).HasColumnName("verified_at");
        builder.Property(grant => grant.RevokedBy).HasColumnName("revoked_by");
        builder.Property(grant => grant.RevokedAt).HasColumnName("revoked_at");
        builder.Ignore(grant => grant.IsVerified);
        builder.Ignore(grant => grant.IsRevoked);
        builder.Ignore(grant => grant.IsEffective);
    }

    private static CommunityRole ParseRole(string name) =>
        CommunityRoles.TryParse(name, out var role) ? role : throw new InvalidOperationException($"Unknown role '{name}' in role_grants.");
}
