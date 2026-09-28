using FieldApp.Application.Abstractions;

namespace FieldApp.Api.Correlation;

public sealed class HttpCorrelationContext(IHttpContextAccessor httpContextAccessor) : ICorrelationContext
{
    public string? CorrelationId => httpContextAccessor.HttpContext?.GetCorrelationId();
}
