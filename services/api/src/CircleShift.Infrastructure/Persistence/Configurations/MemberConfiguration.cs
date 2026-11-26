using CircleShift.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CircleShift.Infrastructure.Persistence.Configurations;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members", table =>
        {
            table.HasCheckConstraint("ck_members_visibility", "visibility IN ('hidden', 'members', 'public')");
            table.HasCheckConstraint("ck_members_languages_count", $"cardinality(languages) <= {LanguageTags.MaxPerMember}");
            table.HasCheckConstraint("ck_members_bio_length", $"char_length(bio) <= {Member.BioMaxLength}");
        });
        builder.HasKey(member => member.Id).HasName("pk_members");

        builder.Property(member => member.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(member => member.DisplayName).HasColumnName("display_name").HasMaxLength(Member.DisplayNameMaxLength).IsRequired();
        builder.Property(member => member.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(Member.TimeZoneIdMaxLength).IsRequired();
        builder.Property(member => member.AdultConfirmedAt).HasColumnName("adult_confirmed_at");
        builder.Property(member => member.CreatedAt).HasColumnName("created_at");
        builder.Property(member => member.UpdatedAt).HasColumnName("updated_at");
        builder.Property(member => member.Bio).HasColumnName("bio").HasMaxLength(Member.BioMaxLength);
        builder.Property(member => member.Languages).HasColumnName("languages").IsRequired();
        builder.Property(member => member.AreaLabel).HasColumnName("area_label").HasMaxLength(Member.AreaLabelMaxLength);
        builder.Property(member => member.Visibility)
            .HasColumnName("visibility")
            .HasConversion(value => value.ToString().ToLowerInvariant(), value => Enum.Parse<ProfileVisibility>(value, true))
            .HasMaxLength(16)
            .IsRequired();

        // Directory and matching queries filter on visibility first.
        builder.HasIndex(member => member.Visibility).HasDatabaseName("ix_members_visibility");
    }
}
