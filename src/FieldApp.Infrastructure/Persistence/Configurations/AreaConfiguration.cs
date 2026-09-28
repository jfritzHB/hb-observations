using FieldApp.Domain.Areas;
using FieldApp.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldApp.Infrastructure.Persistence.Configurations;

internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas", table => table.HasCheckConstraint("CK_Areas_NotOwnParent", "[ParentAreaId] IS NULL OR [ParentAreaId] <> [Id]"));
        builder.HasKey(area => area.Id);
        builder.Property(area => area.Id).ValueGeneratedNever();

        builder.Property(area => area.Name).HasMaxLength(Area.NameMaxLength).IsRequired();
        builder.Property(area => area.Path).HasMaxLength(Area.PathMaxLength).IsRequired();
        builder.Property(area => area.RowVersion).IsRowVersion();

        builder.HasOne<Project>().WithMany().HasForeignKey(area => area.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // The composite key lets the parent foreign key include ProjectId, so the database itself guarantees
        // that a parent area belongs to the same project as its child.
        builder.HasAlternateKey(area => new { area.ProjectId, area.Id });
        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(area => new { area.ProjectId, area.ParentAreaId })
            .HasPrincipalKey(parent => new { parent.ProjectId, parent.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Full paths are unique within a project, so the flattened picker never shows two identical entries.
        builder.HasIndex(area => new { area.ProjectId, area.Path }).IsUnique();
    }
}
