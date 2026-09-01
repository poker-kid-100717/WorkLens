using System.Text.Json;
using System.Text.Json.Serialization;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace WorkLens.Infrastructure.FeedProviders;

/// <summary>
/// Remotive public JSON feed: https://remotive.com/api/remote-jobs — no key required.
/// Supports server-side "search" query param, which we pass the first keyword to,
/// then apply the full keyword set client-side for the final filter.
/// </summary>
public class RemotiveFeedProvider : IJobFeedProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<RemotiveFeedProvider> _logger;

    public JobSource Source => JobSource.Remotive;

    public RemotiveFeedProvider(HttpClient http, ILogger<RemotiveFeedProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobListing>> FetchAsync(IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var url = "https://remotive.com/api/remote-jobs";
        if (keywords.Count > 0)
            url += $"?search={Uri.EscapeDataString(keywords[0])}";

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        var payload = await JsonSerializer.DeserializeAsync<RemotiveResponse>(stream, JsonOpts, ct);
        var jobs = payload?.Jobs ?? new List<RemotiveJob>();

        var now = DateTimeOffset.UtcNow;
        var results = new List<JobListing>();

        foreach (var job in jobs)
        {
            if (keywords.Count > 0 && !MatchesKeywords(job, keywords))
                continue;

            results.Add(new JobListing
            {
                ExternalId = job.Id.ToString(),
                Source = JobSource.Remotive,
                Title = job.Title ?? "Untitled",
                Company = job.CompanyName ?? "Unknown",
                Location = string.IsNullOrWhiteSpace(job.CandidateRequiredLocation) ? "Remote" : job.CandidateRequiredLocation,
                IsRemote = true,
                SalaryMin = null,
                SalaryMax = null,
                SalaryCurrency = null,
                TagsJson = JsonSerializer.Serialize(job.Tags ?? new List<string>()),
                Url = job.Url ?? string.Empty,
                DescriptionHtml = job.Description,
                CompanyLogoUrl = job.CompanyLogoUrl,
                PostedAt = DateTimeOffset.TryParse(job.PublicationDate, out var dt) ? dt : now,
                FetchedAt = now,
                IsActive = true
            });
        }

        _logger.LogInformation("Remotive: fetched {Count} matching listings", results.Count);
        return results;
    }

    private static bool MatchesKeywords(RemotiveJob job, IReadOnlyList<string> keywords)
    {
        var haystack = string.Join(' ', job.Title, job.CompanyName, string.Join(' ', job.Tags ?? new()))
            .ToLowerInvariant();
        return keywords.Any(k => haystack.Contains(k.ToLowerInvariant()));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private class RemotiveResponse
    {
        [JsonPropertyName("jobs")] public List<RemotiveJob>? Jobs { get; set; }
    }

    private class RemotiveJob
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("company_name")] public string? CompanyName { get; set; }
        [JsonPropertyName("company_logo_url")] public string? CompanyLogoUrl { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("candidate_required_location")] public string? CandidateRequiredLocation { get; set; }
        [JsonPropertyName("publication_date")] public string? PublicationDate { get; set; }
    }
}
