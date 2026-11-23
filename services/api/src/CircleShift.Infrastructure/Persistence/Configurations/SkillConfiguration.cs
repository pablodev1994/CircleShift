using CircleShift.Domain.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CircleShift.Infrastructure.Persistence.Configurations;

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        // The database repeats the slug rule so rows inserted outside the API
        // (seeds, manual fixes) cannot break catalog lookups.
        builder.ToTable("skills", table =>
            table.HasCheckConstraint("ck_skills_slug_format", $"slug ~ '{Skill.SlugPattern}'"));
        builder.HasKey(skill => skill.Id).HasName("pk_skills");
        builder.HasIndex(skill => skill.Slug).IsUnique().HasDatabaseName("ux_skills_slug");

        builder.Property(skill => skill.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(skill => skill.Slug).HasColumnName("slug").HasMaxLength(Skill.SlugMaxLength).IsRequired();
        builder.Property(skill => skill.Name).HasColumnName("name").HasMaxLength(Skill.NameMaxLength).IsRequired();
        builder.Property(skill => skill.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(32).IsRequired();
    }
}
