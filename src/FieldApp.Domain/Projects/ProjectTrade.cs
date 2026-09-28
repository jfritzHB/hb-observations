using FieldApp.Domain.Common;
using FieldApp.Domain.Companies;
using FieldApp.Domain.Trades;

namespace FieldApp.Domain.Projects;

/// <summary>
/// Enables a <see cref="Trade"/> on a project and maps it to the company responsible for that trade there.
/// <see cref="ResponsibleCompanyId"/> is the authoritative source of an item's Responsible Company; it is
/// never chosen separately during capture.
/// </summary>
public sealed class ProjectTrade
{
    private ProjectTrade()
    {
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid TradeId { get; private set; }

    /// <summary>Null while the trade is enabled but not yet mapped; such a trade is not available for capture.</summary>
    public Guid? ResponsibleCompanyId { get; private set; }

    public bool IsActive { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static ProjectTrade Create(Guid id, Guid projectId, Guid tradeId, Guid? responsibleCompanyId) => new()
    {
        Id = Guard.RequiredId(id, nameof(Id)),
        ProjectId = Guard.RequiredId(projectId, nameof(ProjectId)),
        TradeId = Guard.RequiredId(tradeId, nameof(TradeId)),
        ResponsibleCompanyId = responsibleCompanyId == Guid.Empty ? null : responsibleCompanyId,
        IsActive = true,
    };

    public void AssignResponsibleCompany(Guid companyId) =>
        ResponsibleCompanyId = Guard.RequiredId(companyId, nameof(ResponsibleCompanyId));

    public void Disable() => IsActive = false;

    public void Enable() => IsActive = true;

    /// <summary>
    /// A trade can be selected for new field capture only when this mapping is enabled, the trade is active, and
    /// it is mapped to an active responsible company.
    /// </summary>
    public bool IsAvailableForCapture(Trade trade, Company? responsibleCompany)
    {
        ArgumentNullException.ThrowIfNull(trade);

        return IsActive
            && trade.Id == TradeId
            && trade.IsActive
            && ResponsibleCompanyId is { } companyId
            && responsibleCompany is not null
            && responsibleCompany.Id == companyId
            && responsibleCompany.IsActive;
    }
}
