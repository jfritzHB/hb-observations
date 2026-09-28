using FieldApp.Application.Abstractions;
using FieldApp.Application.Authorization;
using FieldApp.Application.Common;
using FieldApp.Domain.FieldItems;

namespace FieldApp.Application.Items;

/// <summary>
/// Item-scoped authorization using the concealment policy: an item is visible only through an active membership
/// in its project, and a Draft only to the user who created it; anything invisible is NotFound. A visible item with
/// an operation the member's roles do not allow is Forbidden. Knowing an item ID grants nothing.
/// </summary>
public sealed class ItemAccess(IFieldItemStore items, IProjectMembershipReader memberships, ICurrentUser currentUser)
{
    public async Task<ItemAccessResult> LoadAsync(Guid itemId, ProjectOperation operation, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var item = await items.FindAsync(itemId, cancellationToken);
        if (item is null)
        {
            return ItemAccessResult.Denied(AppError.NotFound("Item not found."));
        }

        var membership = await memberships.FindActiveAsync(item.ProjectId, userId, cancellationToken);
        if (membership is null || (item.LifecycleState == LifecycleState.Draft && item.CreatedByUserId != userId))
        {
            return ItemAccessResult.Denied(AppError.NotFound("Item not found."));
        }

        return ProjectPolicies.Allows(membership.Roles, operation)
            ? new ItemAccessResult(item, null)
            : ItemAccessResult.Denied(AppError.Forbidden());
    }
}

public sealed record ItemAccessResult(FieldItem? Item, AppError? Error)
{
    public static ItemAccessResult Denied(AppError error) => new(null, error);
}
