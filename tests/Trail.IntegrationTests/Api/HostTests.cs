using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Api;

public sealed class HostTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        var response = await _client.GetAsync(path, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await _client.GetAsync("/health/live", Ct);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'none'");
    }

    [Fact]
    public async Task Unknown_routes_return_problem_details()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        problem!.Status.ShouldBe(404);
    }

    [Fact]
    public async Task Api_docs_are_not_exposed_outside_development()
    {
        var response = await _client.GetAsync("/openapi/v1.json", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cors_rejects_unlisted_origins()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health/live");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request, Ct);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}
