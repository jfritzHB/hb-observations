using FieldApp.Application.Common;
using FieldApp.Application.ReferenceData;
using FieldApp.Domain.Areas;
using FieldApp.Domain.Audit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldApp.Infrastructure.Persistence;

internal sealed class EfWriteStore(FieldAppDbContext db) : IAreaStore, IAuditLog, IUnitOfWork
{
    // SQL Server: 2601 duplicate key in unique index, 2627 unique/primary key constraint violation.
    private static readonly int[] _duplicateKeyErrors = [2601, 2627];

    public async Task<IReadOnlyList<Area>> ListForUpdateAsync(Guid projectId, CancellationToken cancellationToken) =>
        await db.Areas.Where(area => area.ProjectId == projectId).ToListAsync(cancellationToken);

    public void Add(Area area) => db.Areas.Add(area);

    public void Add(AuditEvent auditEvent) => db.AuditEvents.Add(auditEvent);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && _duplicateKeyErrors.Contains(sql.Number))
        {
            throw new DuplicateKeyException("A uniqueness constraint rejected the change.", ex);
        }
    }
}
