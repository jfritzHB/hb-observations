using FieldApp.Domain.Areas;
using FieldApp.Domain.Companies;
using FieldApp.Domain.Memberships;
using FieldApp.Domain.Projects;
using FieldApp.Domain.Trades;
using FieldApp.Domain.Users;
using FieldApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static FieldApp.Infrastructure.Seeding.DemoData;

namespace FieldApp.Infrastructure.Seeding;

/// <summary>
/// Inserts the synthetic demo data set. Idempotent: rows that already exist (by deterministic ID) are left
/// untouched. Refuses to run in the Production environment.
/// </summary>
public sealed partial class DemoDataSeeder(FieldAppDbContext db, IHostEnvironment environment, ILogger<DemoDataSeeder> logger)
{
    private static readonly (string Code, string Name, bool IsActive)[] _trades =
    [
        ("DRY", "Drywall", true),
        ("PNT", "Painting", true),
        ("ELE", "Electrical", true),
        ("PLM", "Plumbing", true),
        ("FLR", "Flooring", true),
        ("DHW", "Doors & Hardware", true),
        ("HVAC", "HVAC", true),
        ("CONC", "Concrete", true),
        ("ROOF", "Roofing", true),
        ("GLZ", "Glazing", true),
        ("FIRE", "Fireproofing", false), // Inactive trade: never available for capture.
    ];

    private static readonly (string Name, bool IsActive)[] _companies =
    [
        (Companies.DuneSeaDrywall, true),
        (Companies.TwinSunsPainting, true),
        (Companies.JundlandElectric, true),
        (Companies.BeggarsCanyonPlumbing, true),
        (Companies.DewbackFlooring, true),
        (Companies.MosEspaDoors, true),
        (Companies.HothMechanical, true),
        (Companies.SandcrawlerConcrete, true),
        (Companies.KraytFireproofing, true),
        (Companies.JawaGlass, false), // Inactive company: its mapped trade is not available for capture.
    ];

    private static readonly (string Number, string Name, string TimeZoneId)[] _projects =
    [
        (ProjectNumbers.MosEisley, "Mos Eisley Municipal Center", "America/Phoenix"),
        (ProjectNumbers.Anchorhead, "Anchorhead Water Treatment Plant", "America/Denver"),
        (ProjectNumbers.ToscheStation, "Tosche Station Retrofit", "America/Phoenix"),
    ];

    // (project, trade code, responsible company or null, enabled)
    private static readonly (string Project, string TradeCode, string? Company, bool Enabled)[] _projectTrades =
    [
        (ProjectNumbers.MosEisley, "DRY", Companies.DuneSeaDrywall, true),
        (ProjectNumbers.MosEisley, "PNT", Companies.TwinSunsPainting, true),
        (ProjectNumbers.MosEisley, "ELE", Companies.JundlandElectric, true),
        (ProjectNumbers.MosEisley, "PLM", Companies.BeggarsCanyonPlumbing, true),
        (ProjectNumbers.MosEisley, "FLR", Companies.DewbackFlooring, true),
        (ProjectNumbers.MosEisley, "DHW", Companies.MosEspaDoors, true),
        (ProjectNumbers.MosEisley, "HVAC", Companies.HothMechanical, true),
        (ProjectNumbers.MosEisley, "CONC", Companies.SandcrawlerConcrete, false), // Disabled on this project.
        (ProjectNumbers.MosEisley, "ROOF", null, true), // Enabled but not yet mapped to a company.
        (ProjectNumbers.MosEisley, "GLZ", Companies.JawaGlass, true), // Mapped to an inactive company.
        (ProjectNumbers.MosEisley, "FIRE", Companies.KraytFireproofing, true), // Trade itself is inactive.
        (ProjectNumbers.Anchorhead, "DRY", Companies.DuneSeaDrywall, true),
        (ProjectNumbers.Anchorhead, "ELE", Companies.JundlandElectric, true),
        (ProjectNumbers.Anchorhead, "PLM", Companies.BeggarsCanyonPlumbing, true),
        (ProjectNumbers.ToscheStation, "PNT", Companies.TwinSunsPainting, true),
        (ProjectNumbers.ToscheStation, "HVAC", Companies.HothMechanical, true),
    ];

    // Paths use " / "; parents are listed before children. A leading '-' marks an inactive area.
    private static readonly (string Project, string Path)[] _areas =
    [
        (ProjectNumbers.MosEisley, "Building A"),
        (ProjectNumbers.MosEisley, "Building A / Level 1"),
        (ProjectNumbers.MosEisley, "Building A / Level 1 / Lobby"),
        (ProjectNumbers.MosEisley, "Building A / Level 1 / Conference Room"),
        (ProjectNumbers.MosEisley, "Building A / Level 1 / Restroom"),
        (ProjectNumbers.MosEisley, "Building A / Level 2"),
        (ProjectNumbers.MosEisley, "Building A / Level 2 / Corridor"),
        (ProjectNumbers.MosEisley, "Building A / Level 2 / Office 201"),
        (ProjectNumbers.MosEisley, "Building A / Level 2 / Office 202"),
        (ProjectNumbers.MosEisley, "Building B"),
        (ProjectNumbers.MosEisley, "Building B / Level 1"),
        (ProjectNumbers.MosEisley, "Building B / Level 1 / Mechanical Room"),
        (ProjectNumbers.MosEisley, "Building B / Level 1 / Electrical Room"),
        (ProjectNumbers.MosEisley, "-Building B / Level 1 / Storage (Closed)"),
        (ProjectNumbers.MosEisley, "-Building C (Future Phase)"),
        (ProjectNumbers.MosEisley, "Building C (Future Phase) / Level 1"), // Active, but hidden by its inactive parent.
        (ProjectNumbers.Anchorhead, "Headworks"),
        (ProjectNumbers.Anchorhead, "Headworks / Screening Room"),
        (ProjectNumbers.Anchorhead, "Clarifier 1"),
        (ProjectNumbers.Anchorhead, "Operations Building"),
        (ProjectNumbers.Anchorhead, "Operations Building / Control Room"),
        (ProjectNumbers.ToscheStation, "Main Hall"),
        (ProjectNumbers.ToscheStation, "Main Hall / Vaporator Bay"),
    ];

    // (persona, project, roles, company, active)
    private static readonly (string Persona, string Project, ProjectRoles Roles, string? Company, bool Active)[] _memberships =
    [
        (PersonaKeys.Superintendent, ProjectNumbers.MosEisley, ProjectRoles.Superintendent, null, true),
        (PersonaKeys.Superintendent, ProjectNumbers.Anchorhead, ProjectRoles.Superintendent, null, true),
        (PersonaKeys.ProjectManager, ProjectNumbers.MosEisley, ProjectRoles.ProjectManager, null, true),
        (PersonaKeys.ProjectManager, ProjectNumbers.Anchorhead, ProjectRoles.ProjectManager, null, false),
        (PersonaKeys.Administrator, ProjectNumbers.MosEisley, ProjectRoles.Administrator, null, true),
        (PersonaKeys.Administrator, ProjectNumbers.Anchorhead, ProjectRoles.Administrator, null, true),
        (PersonaKeys.Administrator, ProjectNumbers.ToscheStation, ProjectRoles.Administrator, null, true),
        (PersonaKeys.TradePartner, ProjectNumbers.MosEisley, ProjectRoles.TradePartner, Companies.DuneSeaDrywall, true),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (environment.IsProduction())
        {
            throw new InvalidOperationException("Synthetic demo data must never be seeded into a Production environment.");
        }

        var added = 0;

        foreach (var (code, name, isActive) in _trades)
        {
            added += await AddIfMissingAsync(db.Trades, TradeId(code), () =>
            {
                var trade = Trade.Create(TradeId(code), code, name);
                if (!isActive)
                {
                    trade.Deactivate();
                }

                return trade;
            }, cancellationToken);
        }

        foreach (var (name, isActive) in _companies)
        {
            added += await AddIfMissingAsync(db.Companies, CompanyId(name), () =>
            {
                var company = Company.Create(CompanyId(name), name);
                if (!isActive)
                {
                    company.Deactivate();
                }

                return company;
            }, cancellationToken);
        }

        foreach (var (number, name, timeZoneId) in _projects)
        {
            added += await AddIfMissingAsync(db.Projects, ProjectId(number),
                () => Project.Create(ProjectId(number), number, name, timeZoneId, SeededAt), cancellationToken);
        }

        foreach (var persona in Personas)
        {
            added += await AddIfMissingAsync(db.Users, UserId(persona.Key),
                () => AppUser.Create(UserId(persona.Key), DevelopmentIdentityProvider, persona.Key,
                    $"{persona.DisplayName} ({persona.RoleLabel})", persona.Email, SeededAt), cancellationToken);
        }

        foreach (var (project, tradeCode, company, enabled) in _projectTrades)
        {
            var id = IdFor($"project-trade:{project}:{tradeCode}");
            added += await AddIfMissingAsync(db.ProjectTrades, id, () =>
            {
                var projectTrade = ProjectTrade.Create(id, ProjectId(project), TradeId(tradeCode), company is null ? null : CompanyId(company));
                if (!enabled)
                {
                    projectTrade.Disable();
                }

                return projectTrade;
            }, cancellationToken);
        }

        added += await SeedAreasAsync(cancellationToken);

        foreach (var (persona, project, roles, company, active) in _memberships)
        {
            var id = IdFor($"membership:{persona}:{project}");
            added += await AddIfMissingAsync(db.ProjectMemberships, id, () =>
            {
                var membership = ProjectMembership.Create(id, ProjectId(project), UserId(persona), roles, company is null ? null : CompanyId(company));
                if (!active)
                {
                    membership.Deactivate();
                }

                return membership;
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        LogSeeded(added);
    }

    private async Task<int> SeedAreasAsync(CancellationToken cancellationToken)
    {
        var added = 0;

        foreach (var projectAreas in _areas.GroupBy(entry => entry.Project))
        {
            var projectId = ProjectId(projectAreas.Key);
            var existing = await db.Areas.Where(area => area.ProjectId == projectId).ToListAsync(cancellationToken);
            var byPath = existing.ToDictionary(area => area.Path, StringComparer.OrdinalIgnoreCase);
            var sortOrder = 0;

            foreach (var (_, rawPath) in projectAreas)
            {
                sortOrder += 10;
                var inactive = rawPath.StartsWith('-');
                var path = inactive ? rawPath[1..] : rawPath;
                if (byPath.ContainsKey(path))
                {
                    continue;
                }

                var separator = path.LastIndexOf(Area.PathSeparator, StringComparison.Ordinal);
                var parent = separator < 0 ? null : byPath[path[..separator]];
                var name = separator < 0 ? path : path[(separator + Area.PathSeparator.Length)..];

                var area = Area.Create(AreaId(projectAreas.Key, path), projectId, parent, name, sortOrder);
                if (inactive)
                {
                    area.Deactivate();
                }

                db.Areas.Add(area);
                byPath[path] = area;
                added++;
            }
        }

        return added;
    }

    private static async Task<int> AddIfMissingAsync<T>(DbSet<T> set, Guid id, Func<T> create, CancellationToken cancellationToken)
        where T : class
    {
        if (await set.FindAsync([id], cancellationToken) is not null)
        {
            return 0;
        }

        set.Add(create());
        return 1;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Synthetic demo data seeded: {AddedCount} new row(s)")]
    private partial void LogSeeded(int addedCount);
}
