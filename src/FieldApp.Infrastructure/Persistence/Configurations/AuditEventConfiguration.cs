using FieldApp.Domain.Audit;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldApp.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        // Append-only: the application only ever inserts audit events.
        builder.ToTable("AuditEvents");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.Id).ValueGeneratedNever();

        builder.Property(auditEvent => auditEvent.EntityType).HasMaxLength(AuditEvent.EntityTypeMaxLength).IsRequired();
        builder.Property(auditEvent => auditEvent.Action).HasMaxLength(AuditEvent.ActionMaxLength).IsRequired();
        builder.Property(auditEvent => auditEvent.CorrelationId).HasMaxLength(AuditEvent.CorrelationIdMaxLength);
        builder.Property(auditEvent => auditEvent.Details).HasMaxLength(AuditEvent.DetailsMaxLength);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(auditEvent => auditEvent.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(auditEvent => auditEvent.ProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(auditEvent => new { auditEvent.ProjectId, auditEvent.OccurredAt });
        builder.HasIndex(auditEvent => new { auditEvent.EntityType, auditEvent.EntityId });
    }
}
