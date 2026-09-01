using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace WorkLens.Infrastructure.FeedProviders;

/// <summary>
/// Jobicy's public remote-jobs API. The provider intentionally caches upstream data for
/// one hour because Jobicy's documented fair-use policy says automated checks must not
/// run more frequently than hourly.
/// </summary>
public class JobicyFeedProvider : IJobFeedProvider
{
    private const string Endpoint = "https://jobicy.com/api/v2/remote-jobs?count=200&geo=usa&industry=engineering";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static DateTimeOffset _lastFetchedAt = DateTimeOffset.MinValue;
    private static List<JobicyJob> _cachedJobs = new();

    private readonly HttpClient _http;
    private readonly ILogger<JobicyFeedProvider> _logger;

    public JobSource Source => JobSource.Jobicy;

    public JobicyFeedProvider(HttpClient http, ILogger<JobicyFeedProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobListing>> FetchAsync(IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var jobs = await GetCachedJobsAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var results = jobs
            .Where(j => keywords.Count == 0 || MatchesKeywords(j, keywords))
            .Select(j => MapToListing(j, now))
            .ToList();

        _logger.LogInformation("Jobicy: returning {Count} matching listings", results.Count);
        return results;
    }

    private async Task<List<JobicyJob>> GetCachedJobsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cachedJobs.Count > 0 && now - _lastFetchedAt < CacheDuration)
            return _cachedJobs;

        await CacheLock.WaitAsync(ct);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_cachedJobs.Count > 0 && now - _lastFetchedAt < CacheDuration)
                return _cachedJobs;

            using var response = await _http.GetAsync(Endpoint, ct);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<JobicyResponse>(stream, JsonOpts, ct);
            _cachedJobs = payload?.Jobs ?? new List<JobicyJob>();
            _lastFetchedAt = now;
            return _cachedJobs;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static bool MatchesKeywords(JobicyJob job, IReadOnlyList<string> keywords)
    {
        var haystack = string.Join(' ',
            job.JobTitle,
            job.CompanyName,
            job.JobLevel,
            job.JobExcerpt,
            job.JobDescription,
            string.Join(' ', job.JobIndustry ?? new()),
            string.Join(' ', job.JobType ?? new()))
            .ToLowerInvariant();

        return keywords.Any(k => haystack.Contains(k.ToLowerInvariant()));
    }

    private static JobListing MapToListing(JobicyJob job, DateTimeOffset now)
    {
        var tags = new List<string>();
        if (job.JobIndustry is not null) tags.AddRange(job.JobIndustry);
        if (job.JobType is not null) tags.AddRange(job.JobType);
        if (!string.IsNullOrWhiteSpace(job.JobLevel)) tags.Add(job.JobLevel);
        if (!string.IsNullOrWhiteSpace(job.SalaryPeriod)) tags.Add(job.SalaryPeriod);

        return new JobListing
        {
            ExternalId = job.Id.ToString(CultureInfo.InvariantCulture),
            Source = JobSource.Jobicy,
            Title = job.JobTitle ?? "Untitled",
            Company = job.CompanyName ?? "Unknown",
            Location = string.IsNullOrWhiteSpace(job.JobGeo) ? "Remote" : job.JobGeo,
            IsRemote = true,
            SalaryMin = job.SalaryMin?.ToString("0.##", CultureInfo.InvariantCulture),
            SalaryMax = job.SalaryMax?.ToString("0.##", CultureInfo.InvariantCulture),
            SalaryCurrency = job.SalaryCurrency,
            TagsJson = JsonSerializer.Serialize(tags.Distinct(StringComparer.OrdinalIgnoreCase)),
            Url = job.Url ?? string.Empty,
            DescriptionHtml = job.JobDescription ?? job.JobExcerpt,
            CompanyLogoUrl = job.CompanyLogo,
            PostedAt = job.PubDate ?? now,
            FetchedAt = now,
            IsActive = true
        };
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private class JobicyResponse
    {
        [JsonPropertyName("jobs")] public List<JobicyJob>? Jobs { get; set; }
    }

    private class JobicyJob
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("jobTitle")] public string? JobTitle { get; set; }
        [JsonPropertyName("companyName")] public string? CompanyName { get; set; }
        [JsonPropertyName("companyLogo")] public string? CompanyLogo { get; set; }
        [JsonPropertyName("jobIndustry")] public List<string>? JobIndustry { get; set; }
        [JsonPropertyName("jobType")] public List<string>? JobType { get; set; }
        [JsonPropertyName("jobGeo")] public string? JobGeo { get; set; }
        [JsonPropertyName("jobLevel")] public string? JobLevel { get; set; }
        [JsonPropertyName("jobExcerpt")] public string? JobExcerpt { get; set; }
        [JsonPropertyName("jobDescription")] public string? JobDescription { get; set; }
        [JsonPropertyName("pubDate")] public DateTimeOffset? PubDate { get; set; }
        [JsonPropertyName("salaryMin")] public decimal? SalaryMin { get; set; }
        [JsonPropertyName("salaryMax")] public decimal? SalaryMax { get; set; }
        [JsonPropertyName("salaryCurrency")] public string? SalaryCurrency { get; set; }
        [JsonPropertyName("salaryPeriod")] public string? SalaryPeriod { get; set; }
    }
}
