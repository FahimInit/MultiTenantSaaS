using System.Net;
using System.Net.Http.Json;
using SaaS.API.Controllers.v1;
using SaaS.API.IntegrationTests.Infrastructure;
using Shouldly;

namespace SaaS.API.IntegrationTests;

public sealed class Phase1FoundationTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly HttpClient _client;

    public Phase1FoundationTests(PostgresWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthLive_ShouldReturn200Ok()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_WithPostgreSql_ShouldReturn200Ok()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SystemInfo_ShouldReturnVersionedInfoWithSecurityAndCorrelationHeaders()
    {
        var response = await _client.GetAsync("/api/v1/system/info");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("X-Correlation-Id").ShouldBeTrue();
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");

        var payload = await response.Content.ReadFromJsonAsync<SystemInfoResponse>();
        payload.ShouldNotBeNull();
        payload.Version.ShouldBe("v1");
        payload.Environment.ShouldBe("Development");
    }

    [Fact]
    public async Task SwaggerOpenApiDocument_ShouldReturn200Ok()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.ShouldContain("Multi-Tenant SaaS Management Platform API");
        json.ShouldContain("/api/v1/system/info");
    }
}
