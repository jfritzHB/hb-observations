using FieldApp.Api.Authentication;
using FieldApp.Application.ReferenceData;
using Microsoft.AspNetCore.Mvc;

namespace FieldApp.Api.Endpoints;

/// <summary>Slice 1 reference-data endpoints (docs/05-api-contract.md). Handlers stay thin; rules live in the application layer.</summary>
public static class ReferenceDataEndpoints
{
    public static RouteGroupBuilder MapReferenceDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1")
            .RequireAuthorization(FieldAppPolicies.ApplicationUser)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        api.MapGet("/me", async (ProjectQueries queries, CancellationToken cancellationToken) =>
                await queries.GetCurrentUserAsync(cancellationToken) is { } user
                    ? Results.Ok(user)
                    : Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "No application access."))
            .WithName("GetCurrentUser")
            .WithTags("Me")
            .Produces<CurrentUserDto>();

        var projects = api.MapGroup("/projects").WithTags("Projects");

        projects.MapGet("/", async (ProjectQueries queries, CancellationToken cancellationToken) =>
                TypedResults.Ok(await queries.ListMineAsync(cancellationToken)))
            .WithName("ListProjects")
            .WithSummary("Projects in which the caller has an active membership.");

        projects.MapGet("/{projectId:guid}", async (Guid projectId, ProjectQueries queries, CancellationToken cancellationToken) =>
                (await queries.GetAsync(projectId, cancellationToken)).ToHttp())
            .WithName("GetProject")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapGet("/{projectId:guid}/areas", async (
                Guid projectId,
                [FromQuery] string? search,
                [FromQuery] bool? recent,
                AreaService areas,
                CancellationToken cancellationToken) =>
            {
                // `recent` is accepted per the contract. Recency is derived from a user's captured items, which
                // do not exist until later slices, so the server applies no recency ordering yet.
                _ = recent;
                return (await areas.ListSelectableAsync(projectId, search, cancellationToken)).ToHttp();
            })
            .WithName("ListAreas")
            .WithTags("Areas")
            .WithSummary("Areas selectable for field capture, in hierarchical order, optionally filtered by search terms.")
            .Produces<IReadOnlyList<AreaDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapGet("/{projectId:guid}/areas/{areaId:guid}", async (
                Guid projectId,
                Guid areaId,
                AreaService areas,
                CancellationToken cancellationToken) =>
                (await areas.GetAsync(projectId, areaId, cancellationToken)).ToHttp())
            .WithName("GetArea")
            .WithTags("Areas")
            .Produces<AreaDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPost("/{projectId:guid}/areas", async (
                Guid projectId,
                CreateAreaRequest request,
                AreaService areas,
                CancellationToken cancellationToken) =>
                (await areas.CreateAsync(projectId, request, cancellationToken)).ToHttp(area =>
                    TypedResults.Created($"/api/v1/projects/{projectId}/areas/{area.Id}", area)))
            .WithName("CreateArea")
            .WithTags("Areas")
            .WithSummary("Adds an area (Project Manager or Administrator).")
            .Produces<AreaDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapGet("/{projectId:guid}/trades", async (Guid projectId, TradeQueries trades, CancellationToken cancellationToken) =>
                (await trades.ListForCaptureAsync(projectId, cancellationToken)).ToHttp())
            .WithName("ListTrades")
            .WithTags("Trades")
            .WithSummary("Trades available for field capture, each with its configured Responsible Company.")
            .Produces<IReadOnlyList<CaptureTradeDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapGet("/{projectId:guid}/companies", async (
                Guid projectId,
                [FromQuery] Guid? tradeId,
                TradeQueries trades,
                CancellationToken cancellationToken) =>
                (await trades.ListCompaniesAsync(projectId, tradeId, cancellationToken)).ToHttp())
            .WithName("ListProjectCompanies")
            .WithTags("Companies")
            .Produces<IReadOnlyList<ProjectCompanyDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
