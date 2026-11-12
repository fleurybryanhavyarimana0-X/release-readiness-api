using ReleaseReadiness.Api;
using Xunit;

namespace ReleaseReadiness.Tests;

public class EvaluatorTests
{
    private static readonly DateOnly Today = new(2026, 3, 2);

    private static Release NewRelease(params ReadinessCheck[] checks) =>
        new() { Name = "Partner API 2.4", TargetDate = Today.AddDays(3), Checks = checks.ToList() };

    [Fact]
    public void All_blocking_checks_passed_and_no_open_high_risk_is_go()
    {
        var release = NewRelease(
            new ReadinessCheck("Security scan", "secops", true, CheckStatus.Passed),
            new ReadinessCheck("UAT sign-off", "product", true, CheckStatus.Passed));

        var report = ReadinessEvaluator.Evaluate(release, Today);

        Assert.True(report.Go);
        Assert.Equal(100, report.Score);
        Assert.Empty(report.Blockers);
    }

    [Fact]
    public void Pending_blocking_check_is_no_go()
    {
        var release = NewRelease(
            new ReadinessCheck("Rollback tested", "ops", true, CheckStatus.Pending),
            new ReadinessCheck("Docs", "tw", false, CheckStatus.Passed));

        var report = ReadinessEvaluator.Evaluate(release, Today);

        Assert.False(report.Go);
        Assert.Contains(report.Blockers, b => b.Contains("Rollback tested"));
        Assert.Equal(50, report.Score);
    }

    [Fact]
    public void Open_high_risk_blocks_and_medium_risk_lowers_score()
    {
        var release = NewRelease(new ReadinessCheck("Load test", "sre", true, CheckStatus.Passed));
        release.Risks.Add(new Risk("Settlement window overlap", RiskLevel.High, "pm", false));
        release.Risks.Add(new Risk("Vendor SDK pinned", RiskLevel.Medium, "dev", false));

        var report = ReadinessEvaluator.Evaluate(release, Today);

        Assert.False(report.Go);
        Assert.Equal(90, report.Score);
        Assert.Single(report.Blockers);
    }

    [Fact]
    public void Waiver_without_note_is_flagged()
    {
        var release = NewRelease(new ReadinessCheck("Pen test", "secops", true, CheckStatus.Waived));

        var report = ReadinessEvaluator.Evaluate(release, Today);

        Assert.True(report.Go);
        Assert.Contains(report.Warnings, w => w.Contains("no justification"));
    }

    [Fact]
    public void Release_without_checks_is_never_go()
    {
        Assert.False(ReadinessEvaluator.Evaluate(NewRelease(), Today).Go);
    }
}
