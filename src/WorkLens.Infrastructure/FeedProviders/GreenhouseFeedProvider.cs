using System.Text.Json;
using System.Text.Json.Serialization;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WorkLens.Infrastructure.FeedProviders;

public class GreenhouseOptions
{
    /// <summary>
    /// Board tokens for companies to pull from, e.g. "stripe", "airbnb". Found in each
    /// company's public career page URL: boards.greenhouse.io/{board_token}.
    /// Configure via appsettings.json -> JobFeeds:Greenhouse:BoardTokens.
    /// </summary>
    public List<string> BoardTokens { get; set; } = new();
}

/// <summary>
/// Greenhouse public Job Board API: https://boards-api.greenhouse.io/v1/boards/{token}/jobs
/// No auth required for reads. One call per configured board token, merged together.
/// </summary>
public class GreenhouseFeedProvider : IJobFeedProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<GreenhouseFeedProvider> _logger;
    private readonly GreenhouseOptions _options;

    public JobSource Source => JobSource.Greenhouse;

    public GreenhouseFeedProvider(HttpClient http, ILogger<GreenhouseFeedProvider> logger, IOptions<GreenhouseOptions> options)
    {
        _http = http;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<JobListing>> FetchAsync(IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var results = new List<JobListing>();
        var now = DateTimeOffset.UtcNow;

        foreach (var token in _options.BoardTokens)
        {
            try
            {
                using var response = await _http.GetAsync(
                    $"https://boards-api.greenhouse.io/v1/boards/{Uri.EscapeDataString(token)}/jobs?content=true", ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Greenhouse board '{Token}' returned {Status}", token, response.StatusCode);
                    continue;
                }

                var stream = await response.Content.ReadAsStreamAsync(ct);
                var payload = await JsonSerializer.DeserializeAsync<GreenhouseResponse>(stream, JsonOpts, ct);

                foreach (var job in payload?.Jobs ?? new List<GreenhouseJob>())
                {
                    if (keywords.Count > 0 && !MatchesKeywords(job, keywords))
                        continue;

                    var location = job.Location?.Name;

                    results.Add(new JobListing
                    {
                        ExternalId = $"{token}:{job.Id}",
                        Source = JobSource.Greenhouse,
                        Title = job.Title ?? "Untitled",
                        Company = token,
                        Location = location,
                        IsRemote = location != null && location.Contains("remote", StringComparison.OrdinalIgnoreCase),
                        TagsJson = JsonSerializer.Serialize(job.Departments?.Select(d => d.Name).Where(n => n != null) ?? Enumerable.Empty<string>()),
                        Url = job.AbsoluteUrl ?? string.Empty,
                        DescriptionHtml = job.Content,
                        PostedAt = DateTimeOffset.TryParse(job.UpdatedAt, out var dt) ? dt : now,
                        FetchedAt = now,
                        IsActive = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Greenhouse board '{Token}' fetch failed", token);
            }
        }

        _logger.LogInformation("Greenhouse: fetched {Count} matching listings across {BoardCount} boards", results.Count, _options.BoardTokens.Count);
        return results;
    }

    private static bool MatchesKeywords(GreenhouseJob job, IReadOnlyList<string> keywords)
    {
        var deptNames = job.Departments?.Select(d => d.Name ?? string.Empty) ?? Enumerable.Empty<string>();
        var haystack = string.Join(' ', job.Title, string.Join(' ', deptNames)).ToLowerInvariant();
        return keywords.Any(k => haystack.Contains(k.ToLowerInvariant()));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private class GreenhouseResponse
    {
        [JsonPropertyName("jobs")] public List<GreenhouseJob>? Jobs { get; set; }
    }

    private class GreenhouseJob
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("absolute_url")] public string? AbsoluteUrl { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
        [JsonPropertyName("location")] public GreenhouseLocation? Location { get; set; }
        [JsonPropertyName("departments")] public List<GreenhouseDepartment>? Departments { get; set; }
    }

    private class GreenhouseLocation
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private class GreenhouseDepartment
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
