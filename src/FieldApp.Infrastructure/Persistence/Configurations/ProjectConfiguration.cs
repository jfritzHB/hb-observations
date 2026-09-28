using FieldApp.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldApp.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).ValueGeneratedNever();

        builder.Property(project => project.Number).HasMaxLength(Project.NumberMaxLength).IsRequired();
        builder.Property(project => project.Name).HasMaxLength(Project.NameMaxLength).IsRequired();
        builder.Property(project => project.TimeZoneId).HasMaxLength(Project.TimeZoneIdMaxLength).IsRequired();
        builder.Property(project => project.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(project => project.CreatedAt).IsRequired();
        builder.Property(project => project.RowVersion).IsRowVersion();

        builder.HasIndex(project => project.Number).IsUnique();
    }
}
