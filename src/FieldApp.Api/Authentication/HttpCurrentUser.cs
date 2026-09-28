using FieldApp.Application.Abstractions;

namespace FieldApp.Api.Authentication;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(FieldAppClaimTypes.ApplicationUserId)?.Value, out var id)
            ? id
            : null;
}
