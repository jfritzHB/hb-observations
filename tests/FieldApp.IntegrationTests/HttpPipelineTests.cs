using System.Net;
using System.Text.Json;
using FieldApp.IntegrationTests.Infrastructure;

namespace FieldApp.IntegrationTests;

public sealed class HttpPipelineTests(FieldAppFactory factory) : IClassFixture<FieldAppFactory>
{
    private const string CorrelationHeader = "X-Correlation-Id";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Valid_incoming_correlation_id_is_echoed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationHeader, "field-walk-42");

        var response = await client.SendAsync(request, CancellationToken);

        Assert.Equal("field-walk-42", Assert.Single(response.Headers.GetValues(CorrelationHeader)));
    }

    [Fact]
    public async Task Missing_correlation_id_is_generated()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", CancellationToken);

        var correlationId = Assert.Single(response.Headers.GetValues(CorrelationHeader));
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Theory]
    [InlineData("contains spaces")]
    [InlineData("<script>")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Malformed_correlation_id_is_replaced_not_reflected(string malformed)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(CorrelationHeader, malformed);

        var response = await client.SendAsync(request, CancellationToken);

        var correlationId = Assert.Single(response.Headers.GetValues(CorrelationHeader));
        Assert.NotEqual(malformed, correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task Unknown_api_route_returns_problem_details_with_correlation_id()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/does-not-exist");
        request.Headers.Add(CorrelationHeader, "problem-test-1");

        var response = await client.SendAsync(request, CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("problem-test-1", problem.RootElement.GetProperty("correlationId").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/projects/123/items")]
    public async Task Client_routes_are_served_the_spa_shell(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(FieldAppFactory.SpaShellMarker, await response.Content.ReadAsStringAsync(CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_document_is_published_in_development()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString(), StringComparison.Ordinal);
        Assert.Equal("HB Observations API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
    }
}
