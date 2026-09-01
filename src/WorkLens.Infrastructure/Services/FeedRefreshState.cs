using System.Collections.Concurrent;
using WorkLens.Core.Enums;

namespace WorkLens.Infrastructure.Services;

/// <summary>
/// In-memory record of the last refresh outcome per source, so the API can report
/// feed health ("last refreshed 3s ago", "Greenhouse failing") without hitting the DB.
/// Registered as a singleton.
/// </summary>
public class FeedRefreshState
{
    private readonly ConcurrentDictionary<JobSource, SourceStatus> _statuses = new();

    public DateTimeOffset? LastFullRefreshAt { get; set; }

    public void RecordSuccess(JobSource source, int count)
    {
        _statuses[source] = new SourceStatus(true, DateTimeOffset.UtcNow, count, null);
    }

    public void RecordFailure(JobSource source, string error)
    {
        var previous = _statuses.TryGetValue(source, out var existing) ? existing : null;
        _statuses[source] = new SourceStatus(false, previous?.LastFetchedAt, previous?.ListingCount ?? 0, error);
    }

    public IReadOnlyDictionary<JobSource, SourceStatus> Snapshot() => _statuses;

    public record SourceStatus(bool LastFetchSucceeded, DateTimeOffset? LastFetchedAt, int ListingCount, string? LastError);
}
