using CircleShift.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CircleShift.Infrastructure.Persistence.Configurations;

internal sealed class MemberBlockConfiguration : IEntityTypeConfiguration<MemberBlock>
{
    public void Configure(EntityTypeBuilder<MemberBlock> builder)
    {
        builder.ToTable("member_blocks", table =>
            table.HasCheckConstraint("ck_member_blocks_not_self", "blocker_id <> blocked_id"));

        // One row per pair; blocking twice is a no-op.
        builder.HasKey(block => new { block.BlockerId, block.BlockedId }).HasName("pk_member_blocks");

        // Visibility checks look a pair up from either side.
        builder.HasIndex(block => block.BlockedId).HasDatabaseName("ix_member_blocks_blocked_id");

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(block => block.BlockerId)
            .HasConstraintName("fk_member_blocks_blocker")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(block => block.BlockedId)
            .HasConstraintName("fk_member_blocks_blocked")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(block => block.BlockerId).HasColumnName("blocker_id");
        builder.Property(block => block.BlockedId).HasColumnName("blocked_id");
        builder.Property(block => block.CreatedAt).HasColumnName("created_at");
    }
}
