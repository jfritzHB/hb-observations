using FieldApp.Application.Common;
using FieldApp.Application.Items;
using FieldApp.Application.ReferenceData;
using FieldApp.Domain.Areas;
using FieldApp.Domain.Audit;
using FieldApp.Domain.Common;
using FieldApp.Domain.FieldItems;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FieldApp.Infrastructure.Persistence;

internal sealed class EfWriteStore(FieldAppDbContext db) : IAreaStore, IAuditLog, IUnitOfWork, IFieldItemStore
{
    // SQL Server: 2601 duplicate key in unique index, 2627 unique/primary key constraint violation.
    private static readonly int[] _duplicateKeyErrors = [2601, 2627];

    public async Task<IReadOnlyList<Area>> ListForUpdateAsync(Guid projectId, CancellationToken cancellationToken) =>
        await db.Areas.Where(area => area.ProjectId == projectId).ToListAsync(cancellationToken);

    public void Add(Area area) => db.Areas.Add(area);

    public void Add(AuditEvent auditEvent) => db.AuditEvents.Add(auditEvent);

    public void Add(FieldItem item) => db.FieldItems.Add(item);

    public void Add(IdempotencyRecord record) => db.IdempotencyRecords.Add(record);

    public Task<FieldItem?> FindAsync(Guid itemId, CancellationToken cancellationToken) =>
        db.FieldItems.Include(item => item.Photos).SingleOrDefaultAsync(item => item.Id == itemId, cancellationToken);

    public Task<FieldItem?> FindByClientDraftAsync(Guid projectId, Guid createdByUserId, Guid clientDraftId, CancellationToken cancellationToken) =>
        db.FieldItems.Include(item => item.Photos).SingleOrDefaultAsync(
            item => item.ProjectId == projectId && item.CreatedByUserId == createdByUserId && item.ClientDraftId == clientDraftId,
            cancellationToken);

    public Task<IdempotencyRecord?> FindIdempotencyRecordAsync(Guid userId, string scope, string key, CancellationToken cancellationToken) =>
        db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            record => record.UserId == userId && record.Scope == scope && record.Key == key,
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Forget the failed changes so a follow-up read sees the winning state.
            db.ChangeTracker.Clear();
            throw new ConcurrencyConflictException("The data changed since it was read.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && _duplicateKeyErrors.Contains(sql.Number))
        {
            db.ChangeTracker.Clear();
            throw new DuplicateKeyException("A uniqueness constraint rejected the change.", ex);
        }
    }
}
