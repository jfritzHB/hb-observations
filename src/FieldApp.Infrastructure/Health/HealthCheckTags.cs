namespace FieldApp.Infrastructure.Health;

public static class HealthCheckTags
{
    /// <summary>Checks that must pass before the instance receives traffic (<c>/health/ready</c>).</summary>
    public const string Ready = "ready";
}
