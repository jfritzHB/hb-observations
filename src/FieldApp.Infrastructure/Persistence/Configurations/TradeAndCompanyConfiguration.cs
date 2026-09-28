using FieldApp.Domain.Companies;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldApp.Infrastructure.Persistence.Configurations;

internal sealed class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("Trades");
        builder.HasKey(trade => trade.Id);
        builder.Property(trade => trade.Id).ValueGeneratedNever();

        builder.Property(trade => trade.Code).HasMaxLength(Trade.CodeMaxLength).IsRequired();
        builder.Property(trade => trade.Name).HasMaxLength(Trade.NameMaxLength).IsRequired();
        builder.Property(trade => trade.RowVersion).IsRowVersion();

        builder.HasIndex(trade => trade.Code).IsUnique();
        builder.HasIndex(trade => trade.Name).IsUnique();
    }
}

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(company => company.Id);
        builder.Property(company => company.Id).ValueGeneratedNever();

        builder.Property(company => company.Name).HasMaxLength(Company.NameMaxLength).IsRequired();
        builder.Property(company => company.RowVersion).IsRowVersion();

        builder.HasIndex(company => company.Name).IsUnique();
    }
}

internal sealed class ProjectTradeConfiguration : IEntityTypeConfiguration<ProjectTrade>
{
    public void Configure(EntityTypeBuilder<ProjectTrade> builder)
    {
        builder.ToTable("ProjectTrades");
        builder.HasKey(projectTrade => projectTrade.Id);
        builder.Property(projectTrade => projectTrade.Id).ValueGeneratedNever();
        builder.Property(projectTrade => projectTrade.RowVersion).IsRowVersion();

        builder.HasOne<Project>().WithMany().HasForeignKey(projectTrade => projectTrade.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Trade>().WithMany().HasForeignKey(projectTrade => projectTrade.TradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(projectTrade => projectTrade.ResponsibleCompanyId).OnDelete(DeleteBehavior.Restrict);

        // A trade is enabled at most once per project, so it has exactly one responsible company there.
        builder.HasIndex(projectTrade => new { projectTrade.ProjectId, projectTrade.TradeId }).IsUnique();
    }
}
