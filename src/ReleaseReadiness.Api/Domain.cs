namespace ReleaseReadiness.Api;

public enum CheckStatus { Pending, Passed, Failed, Waived }
public enum RiskLevel { Low, Medium, High, Critical }

public sealed record ReadinessCheck(string Name, string Owner, bool Blocking, CheckStatus Status, string? Note = null);

public sealed record Risk(string Title, RiskLevel Level, string Owner, bool Mitigated);

public sealed class Release
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required DateOnly TargetDate { get; init; }
    public List<ReadinessCheck> Checks { get; init; } = new();
    public List<Risk> Risks { get; init; } = new();
}

public sealed record ReadinessReport(
    Guid ReleaseId,
    string Release,
    bool Go,
    int Score,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings);

public static class ReadinessEvaluator
{
    /// <summary>
    /// Go/no-go rule used in change-advisory sessions:
    /// no blocking check failed or pending, and no unmitigated High or Critical risk.
    /// Score is the share of checks passed or waived, minus 10 points per open Medium risk (floor 0).
    /// </summary>
    public static ReadinessReport Evaluate(Release release, DateOnly today)
    {
        var blockers = new List<string>();
        var warnings = new List<string>();

        foreach (var check in release.Checks)
        {
            var done = check.Status is CheckStatus.Passed or CheckStatus.Waived;
            if (check.Blocking && !done)
            {
                blockers.Add($"Blocking check '{check.Name}' is {check.Status} (owner {check.Owner})");
            }
            else if (!done)
            {
                warnings.Add($"Check '{check.Name}' is {check.Status}");
            }

            if (check.Status == CheckStatus.Waived && string.IsNullOrWhiteSpace(check.Note))
            {
                warnings.Add($"Waiver on '{check.Name}' has no justification");
            }
        }

        foreach (var risk in release.Risks.Where(r => !r.Mitigated))
        {
            if (risk.Level >= RiskLevel.High)
            {
                blockers.Add($"{risk.Level} risk open: {risk.Title} (owner {risk.Owner})");
            }
            else if (risk.Level == RiskLevel.Medium)
            {
                warnings.Add($"Medium risk open: {risk.Title}");
            }
        }

        if (release.TargetDate < today)
        {
            warnings.Add($"Target date {release.TargetDate:yyyy-MM-dd} is in the past");
        }

        var total = release.Checks.Count;
        var ok = release.Checks.Count(c => c.Status is CheckStatus.Passed or CheckStatus.Waived);
        var score = total == 0 ? 0 : (int)Math.Round(100.0 * ok / total);
        var openMedium = release.Risks.Count(r => !r.Mitigated && r.Level == RiskLevel.Medium);
        score = Math.Max(0, score - 10 * openMedium);

        return new ReadinessReport(release.Id, release.Name, blockers.Count == 0 && total > 0, score, blockers, warnings);
    }
}
