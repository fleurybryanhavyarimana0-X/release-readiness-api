using System.Collections.Concurrent;

namespace ReleaseReadiness.Api;

public interface IReleaseStore
{
    IEnumerable<Release> All();
    Release? Get(Guid id);
    Release Add(Release release);
    bool UpsertCheck(Guid id, ReadinessCheck check);
    bool AddRisk(Guid id, Risk risk);
}

public sealed class InMemoryReleaseStore : IReleaseStore
{
    private readonly ConcurrentDictionary<Guid, Release> _items = new();
    private readonly object _gate = new();

    public IEnumerable<Release> All() => _items.Values.OrderBy(r => r.TargetDate);

    public Release? Get(Guid id) => _items.TryGetValue(id, out var release) ? release : null;

    public Release Add(Release release)
    {
        _items[release.Id] = release;
        return release;
    }

    public bool UpsertCheck(Guid id, ReadinessCheck check)
    {
        if (!_items.TryGetValue(id, out var release))
        {
            return false;
        }

        lock (_gate)
        {
            var index = release.Checks.FindIndex(c => string.Equals(c.Name, check.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                release.Checks[index] = check;
            }
            else
            {
                release.Checks.Add(check);
            }
        }

        return true;
    }

    public bool AddRisk(Guid id, Risk risk)
    {
        if (!_items.TryGetValue(id, out var release))
        {
            return false;
        }

        lock (_gate)
        {
            release.Risks.Add(risk);
        }

        return true;
    }
}
