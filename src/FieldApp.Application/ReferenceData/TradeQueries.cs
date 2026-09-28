using FieldApp.Application.Authorization;
using FieldApp.Application.Common;

namespace FieldApp.Application.ReferenceData;

public sealed class TradeQueries(IReferenceDataReader reader, ProjectAuthorizer authorizer)
{
    /// <summary>
    /// Trades selectable for field capture on a project, each with its Responsible Company resolved from the
    /// Project Trade mapping. Disabled, inactive or unmapped trades are excluded.
    /// </summary>
    public async Task<Result<IReadOnlyList<CaptureTradeDto>>> ListForCaptureAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var authorization = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ViewReferenceData, cancellationToken);
        if (!authorization.IsGranted)
        {
            return authorization.Error!;
        }

        var mappings = await reader.ListProjectTradesAsync(projectId, cancellationToken);

        IReadOnlyList<CaptureTradeDto> result = [.. mappings
            .Where(mapping => mapping.IsAvailableForCapture)
            .OrderBy(mapping => mapping.Trade.Name, StringComparer.OrdinalIgnoreCase)
            .Select(mapping => new CaptureTradeDto(
                mapping.Trade.Id,
                mapping.Trade.Code,
                mapping.Trade.Name,
                new ResponsibleCompanyDto(mapping.ResponsibleCompany!.Id, mapping.ResponsibleCompany.Name)))];

        return Result.Success(result);
    }

    /// <summary>Companies responsible for capture-available trades on the project, optionally for one trade.</summary>
    public async Task<Result<IReadOnlyList<ProjectCompanyDto>>> ListCompaniesAsync(
        Guid projectId,
        Guid? tradeId,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizer.AuthorizeAsync(projectId, ProjectOperation.ViewReferenceData, cancellationToken);
        if (!authorization.IsGranted)
        {
            return authorization.Error!;
        }

        var mappings = await reader.ListProjectTradesAsync(projectId, cancellationToken);

        IReadOnlyList<ProjectCompanyDto> result = [.. mappings
            .Where(mapping => mapping.IsAvailableForCapture)
            .Where(mapping => tradeId is null || mapping.Trade.Id == tradeId)
            .GroupBy(mapping => mapping.ResponsibleCompany!.Id)
            .Select(group => new ProjectCompanyDto(
                group.Key,
                group.First().ResponsibleCompany!.Name,
                [.. group.Select(mapping => mapping.Trade.Id)]))
            .OrderBy(company => company.Name, StringComparer.OrdinalIgnoreCase)];

        return Result.Success(result);
    }
}
