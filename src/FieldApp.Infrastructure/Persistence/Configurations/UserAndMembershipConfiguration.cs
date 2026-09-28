using FieldApp.Domain.Companies;
using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldApp.Infrastructure.Persistence.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.IdentityProvider).HasMaxLength(AppUser.IdentityProviderMaxLength).IsRequired();
        builder.Property(user => user.IdentitySubject).HasMaxLength(AppUser.IdentitySubjectMaxLength).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(AppUser.DisplayNameMaxLength).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(AppUser.EmailMaxLength);
        builder.Property(user => user.RowVersion).IsRowVersion();

        // One application user per external identity.
        builder.HasIndex(user => new { user.IdentityProvider, user.IdentitySubject }).IsUnique();
    }
}

internal sealed class ProjectMembershipConfiguration : IEntityTypeConfiguration<ProjectMembership>
{
    public void Configure(EntityTypeBuilder<ProjectMembership> builder)
    {
        builder.ToTable("ProjectMemberships", table =>
        {
            table.HasCheckConstraint("CK_ProjectMemberships_HasRole", "[Roles] > 0 AND [Roles] <= 15");

            // Trade Partner (8) memberships are company scoped and cannot be combined with employee roles.
            table.HasCheckConstraint(
                "CK_ProjectMemberships_TradePartnerScope",
                "([Roles] & 8) = 0 OR ([Roles] = 8 AND [CompanyId] IS NOT NULL)");
        });

        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).ValueGeneratedNever();
        builder.Property(membership => membership.Roles).HasConversion<int>();
        builder.Property(membership => membership.RowVersion).IsRowVersion();

        builder.HasOne<Project>().WithMany().HasForeignKey(membership => membership.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(membership => membership.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(membership => membership.CompanyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new { membership.ProjectId, membership.UserId }).IsUnique();
        builder.HasIndex(membership => new { membership.UserId, membership.IsActive });
    }
}
