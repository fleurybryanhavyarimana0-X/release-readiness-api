using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using ReleaseReadiness.Api;
using Xunit;

namespace ReleaseReadiness.Tests;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_release_add_checks_and_get_readiness()
    {
        var create = await _client.PostAsJsonAsync("/releases", new { name = "Mobile driver app 5.1", targetDate = "2030-01-15" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var release = await create.Content.ReadFromJsonAsync<Release>(Json);
        Assert.NotNull(release);

        var pending = new ReadinessCheck("UAT sign-off", "product", true, CheckStatus.Pending);
        var put = await _client.PutAsJsonAsync($"/releases/{release!.Id}/checks", pending, Json);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var report = await _client.GetFromJsonAsync<ReadinessReport>($"/releases/{release.Id}/readiness", Json);
        Assert.False(report!.Go);

        var passed = pending with { Status = CheckStatus.Passed };
        await _client.PutAsJsonAsync($"/releases/{release.Id}/checks", passed, Json);
        report = await _client.GetFromJsonAsync<ReadinessReport>($"/releases/{release.Id}/readiness", Json);
        Assert.True(report!.Go);
        Assert.Equal(100, report.Score);
    }

    [Fact]
    public async Task Unknown_release_returns_404()
    {
        var response = await _client.GetAsync($"/releases/{Guid.NewGuid()}/readiness");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Blank_name_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/releases", new { name = " ", targetDate = "2030-01-15" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
