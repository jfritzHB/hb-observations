using FieldApp.Domain.Areas;
using FieldApp.Domain.Capture;
using FieldApp.Domain.Common;
using FieldApp.Domain.Companies;
using FieldApp.Domain.FieldItems;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;
using FieldApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldApp.Infrastructure.Persistence.Configurations;

internal sealed class FieldItemConfiguration : IEntityTypeConfiguration<FieldItem>
{
    public void Configure(EntityTypeBuilder<FieldItem> builder)
    {
        builder.ToTable("FieldItems", table =>
            // A published item must have meaningful location: a structured Area and/or a Location Detail.
            table.HasCheckConstraint(
                "CK_FieldItems_PublishedHasLocation",
                "[LifecycleState] <> 'Published' OR [AreaId] IS NOT NULL OR [LocationDetail] IS NOT NULL"));

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Ignore(item => item.Location);
        builder.Ignore(item => item.PrimaryPhoto);

        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.LifecycleState).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.AreaPathSnapshot).HasMaxLength(Area.PathMaxLength);
        builder.Property(item => item.LocationDetail).HasMaxLength(ItemLocation.LocationDetailMaxLength);
        builder.Property(item => item.TradeNameSnapshot).HasMaxLength(FieldItem.TradeNameSnapshotMaxLength).IsRequired();
        builder.Property(item => item.ResponsibleCompanyNameSnapshot).HasMaxLength(FieldItem.CompanyNameSnapshotMaxLength).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Trade>().WithMany().HasForeignKey(item => item.TradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(item => item.ResponsibleCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Composite key to Areas(ProjectId, Id): the database guarantees an item's Area is in the item's project.
        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(item => new { item.ProjectId, item.AreaId })
            .HasPrincipalKey(area => new { area.ProjectId, area.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(item => item.Photos)
            .WithOne()
            .HasForeignKey(photo => photo.FieldItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);

        // One server item per local draft per creator: a retried creation can always find its item.
        builder.HasIndex(item => new { item.ProjectId, item.CreatedByUserId, item.ClientDraftId }).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.LifecycleState });
    }
}

internal sealed class FieldItemPhotoConfiguration : IEntityTypeConfiguration<FieldItemPhoto>
{
    public void Configure(EntityTypeBuilder<FieldItemPhoto> builder)
    {
        builder.ToTable("FieldItemPhotos", table =>
        {
            table.HasCheckConstraint("CK_FieldItemPhotos_PositiveSize", "[ByteLength] > 0");
            table.HasCheckConstraint(
                "CK_FieldItemPhotos_FinalizedHasMetadata",
                "[Status] <> 'Finalized' OR ([Width] > 0 AND [Height] > 0 AND [ThumbnailBlobKey] IS NOT NULL AND [FinalizedAt] IS NOT NULL)");
        });

        builder.HasKey(photo => photo.Id);
        builder.Property(photo => photo.Id).ValueGeneratedNever();
        builder.Ignore(photo => photo.IsActive);

        builder.Property(photo => photo.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(photo => photo.BlobKey).HasMaxLength(FieldItemPhoto.BlobKeyMaxLength).IsRequired();
        builder.Property(photo => photo.ThumbnailBlobKey).HasMaxLength(FieldItemPhoto.BlobKeyMaxLength);
        builder.Property(photo => photo.MediaType).HasMaxLength(50).IsRequired();
        builder.Property(photo => photo.Sha256).HasMaxLength(FieldItemPhoto.Sha256Base64Length).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(photo => photo.RowVersion).IsRowVersion();

        builder.HasOne<AppUser>().WithMany().HasForeignKey(photo => photo.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(photo => photo.BlobKey).IsUnique();

        // At most one active primary photo per item, even under concurrent requests.
        builder.HasIndex(photo => photo.FieldItemId)
            .IsUnique()
            .HasFilter("[IsPrimary] = 1 AND [Status] <> 'Abandoned'")
            .HasDatabaseName("UX_FieldItemPhotos_OnePrimary");
    }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.Scope).HasMaxLength(IdempotencyRecord.ScopeMaxLength).IsRequired();
        builder.Property(record => record.Key).HasMaxLength(IdempotencyRecord.KeyMaxLength).IsUnicode(false).IsRequired();
        builder.Property(record => record.RequestFingerprint).HasMaxLength(IdempotencyRecord.FingerprintLength).IsFixedLength().IsUnicode(false).IsRequired();

        builder.HasOne<AppUser>().WithMany().HasForeignKey(record => record.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(record => new { record.UserId, record.Scope, record.Key }).IsUnique();
    }
}
