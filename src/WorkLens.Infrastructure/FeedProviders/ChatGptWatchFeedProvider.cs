using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace WorkLens.Infrastructure.FeedProviders;

/// <summary>
/// Pulls curated matches produced by the user's ChatGPT job-watch automations from a
/// dedicated GitHub data branch. This lets the local WorkLens instance consume the same
/// strong-match discoveries without exposing the local machine to the public internet.
/// </summary>
public class ChatGptWatchFeedProvider : IJobFeedProvider
{
    private const string Endpoint = "https://raw.githubusercontent.com/poker-kid-100717/WorkLens/job-watch-data/data/job-watch-feed.json";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static DateTimeOffset _lastFetchedAt = DateTimeOffset.MinValue;
    private static WatchFeed _cachedFeed = new();

    private readonly HttpClient _http;
    private readonly ILogger<ChatGptWatchFeedProvider> _logger;

    public JobSource Source => JobSource.ChatGptWatch;

    public ChatGptWatchFeedProvider(HttpClient http, ILogger<ChatGptWatchFeedProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobListing>> FetchAsync(IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var feed = await GetCachedFeedAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var results = (feed.Jobs ?? new List<WatchJob>())
            .Where(j => !string.IsNullOrWhiteSpace(j.Title) && !string.IsNullOrWhiteSpace(j.Url))
            .Select(j => MapToListing(j, now))
            .ToList();

        _logger.LogInformation("ChatGPT Job Watch: returning {Count} curated listings", results.Count);
        return results;
    }

    private async Task<WatchFeed> GetCachedFeedAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastFetchedAt != DateTimeOffset.MinValue && now - _lastFetchedAt < CacheDuration)
            return _cachedFeed;

        await CacheLock.WaitAsync(ct);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_lastFetchedAt != DateTimeOffset.MinValue && now - _lastFetchedAt < CacheDuration)
                return _cachedFeed;

            using var response = await _http.GetAsync(Endpoint, ct);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(ct);
            _cachedFeed = await JsonSerializer.DeserializeAsync<WatchFeed>(stream, JsonOpts, ct) ?? new WatchFeed();
            _lastFetchedAt = now;
            return _cachedFeed;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static JobListing MapToListing(WatchJob job, DateTimeOffset now)
    {
        var tags = new List<string> { "ChatGPT Job Watch", "Curated match" };
        if (job.Tags is not null) tags.AddRange(job.Tags);

        var salaryMin = JsonSalaryToString(job.SalaryMin);
        var salaryMax = JsonSalaryToString(job.SalaryMax);
        var salaryCeiling = ParseSalary(salaryMax) ?? ParseSalary(salaryMin);

        if (salaryCeiling >= 160_000)
            tags.Add("$160k+ watch");
        else if (salaryCeiling >= 140_000)
            tags.Add("$140k+ watch");

        var externalSeed = !string.IsNullOrWhiteSpace(job.Id) ? job.Id! : job.Url!;

        return new JobListing
        {
            ExternalId = StableId(externalSeed),
            Source = JobSource.ChatGptWatch,
            Title = job.Title ?? "Untitled",
            Company = job.Company ?? "Unknown",
            Location = string.IsNullOrWhiteSpace(job.Location) ? "Remote" : job.Location,
            IsRemote = job.IsRemote ?? true,
            SalaryMin = salaryMin,
            SalaryMax = salaryMax,
            SalaryCurrency = string.IsNullOrWhiteSpace(job.SalaryCurrency) ? "USD" : job.SalaryCurrency,
            TagsJson = JsonSerializer.Serialize(tags.Distinct(StringComparer.OrdinalIgnoreCase)),
            Url = job.Url!,
            DescriptionHtml = job.WhyFit,
            CompanyLogoUrl = null,
            PostedAt = job.FoundAt ?? now,
            FetchedAt = now,
            IsActive = true
        };
    }

    private static string StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? JsonSalaryToString(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetDecimal(out var number) ? number.ToString("0.##", CultureInfo.InvariantCulture) : value.GetRawText();
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();
        return null;
    }

    private static decimal? ParseSalary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private class WatchFeed
    {
        [JsonPropertyName("updatedAt")] public DateTimeOffset? UpdatedAt { get; set; }
        [JsonPropertyName("jobs")] public List<WatchJob>? Jobs { get; set; }
    }

    private class WatchJob
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("company")] public string? Company { get; set; }
        [JsonPropertyName("location")] public string? Location { get; set; }
        [JsonPropertyName("isRemote")] public bool? IsRemote { get; set; }
        [JsonPropertyName("salaryMin")] public JsonElement SalaryMin { get; set; }
        [JsonPropertyName("salaryMax")] public JsonElement SalaryMax { get; set; }
        [JsonPropertyName("salaryCurrency")] public string? SalaryCurrency { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("whyFit")] public string? WhyFit { get; set; }
        [JsonPropertyName("foundAt")] public DateTimeOffset? FoundAt { get; set; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    }
}
