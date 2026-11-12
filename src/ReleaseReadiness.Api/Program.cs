using System.Text.Json.Serialization;
using ReleaseReadiness.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IReleaseStore, InMemoryReleaseStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var releases = app.MapGroup("/releases");

releases.MapGet("/", (IReleaseStore store) => store.All());

releases.MapPost("/", (CreateRelease body, IReleaseStore store) =>
{
    if (string.IsNullOrWhiteSpace(body.Name))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = new[] { "Name is required." } });
    }

    var release = store.Add(new Release { Name = body.Name.Trim(), TargetDate = body.TargetDate });
    return Results.Created($"/releases/{release.Id}", release);
});

releases.MapGet("/{id:guid}", (Guid id, IReleaseStore store) =>
    store.Get(id) is { } release ? Results.Ok(release) : Results.NotFound());

releases.MapPut("/{id:guid}/checks", (Guid id, ReadinessCheck check, IReleaseStore store) =>
{
    if (string.IsNullOrWhiteSpace(check.Name))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = new[] { "Check name is required." } });
    }

    return store.UpsertCheck(id, check) ? Results.NoContent() : Results.NotFound();
});

releases.MapPost("/{id:guid}/risks", (Guid id, Risk risk, IReleaseStore store) =>
    store.AddRisk(id, risk) ? Results.NoContent() : Results.NotFound());

releases.MapGet("/{id:guid}/readiness", (Guid id, IReleaseStore store, TimeProvider clock) =>
{
    if (store.Get(id) is not { } release)
    {
        return Results.NotFound();
    }

    var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    return Results.Ok(ReadinessEvaluator.Evaluate(release, today));
});

app.Run();

public sealed record CreateRelease(string Name, DateOnly TargetDate);

public partial class Program { }
