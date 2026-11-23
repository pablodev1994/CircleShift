using CircleShift.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CircleShift.Infrastructure.Persistence.Configurations;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members");
        builder.HasKey(member => member.Id).HasName("pk_members");

        builder.Property(member => member.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(member => member.DisplayName).HasColumnName("display_name").HasMaxLength(Member.DisplayNameMaxLength).IsRequired();
        builder.Property(member => member.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(Member.TimeZoneIdMaxLength).IsRequired();
        builder.Property(member => member.AdultConfirmedAt).HasColumnName("adult_confirmed_at");
        builder.Property(member => member.CreatedAt).HasColumnName("created_at");
    }
}
