using System.Text.Json;
using System.Text.Json.Serialization;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace WorkLens.Infrastructure.FeedProviders;

/// <summary>
/// RemoteOK public JSON feed: https://remoteok.com/api — no key required.
/// The first array element is a legacy "metadata" record and is skipped.
/// </summary>
public class RemoteOkFeedProvider : IJobFeedProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<RemoteOkFeedProvider> _logger;

    public JobSource Source => JobSource.RemoteOk;

    public RemoteOkFeedProvider(HttpClient http, ILogger<RemoteOkFeedProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobListing>> FetchAsync(IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var results = new List<JobListing>();

        using var response = await _http.GetAsync("https://remoteok.com/api", ct);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        var raw = await JsonSerializer.DeserializeAsync<List<RemoteOkJob>>(stream, JsonOpts, ct) ?? new();

        var now = DateTimeOffset.UtcNow;

        foreach (var job in raw)
        {
            // Skip the legacy metadata row and any record missing an id.
            if (string.IsNullOrWhiteSpace(job.Id) || string.IsNullOrWhiteSpace(job.Position))
                continue;

            if (keywords.Count > 0 && !MatchesKeywords(job, keywords))
                continue;

            results.Add(new JobListing
            {
                ExternalId = job.Id!,
                Source = JobSource.RemoteOk,
                Title = job.Position ?? "Untitled",
                Company = job.Company ?? "Unknown",
                Location = string.IsNullOrWhiteSpace(job.Location) ? "Remote" : job.Location,
                IsRemote = true,
                SalaryMin = job.SalaryMin?.ToString(),
                SalaryMax = job.SalaryMax?.ToString(),
                SalaryCurrency = job.SalaryMin.HasValue || job.SalaryMax.HasValue ? "USD" : null,
                TagsJson = JsonSerializer.Serialize(job.Tags ?? new List<string>()),
                Url = string.IsNullOrWhiteSpace(job.Url) ? $"https://remoteok.com/remote-jobs/{job.Id}" : job.Url!,
                DescriptionHtml = job.Description,
                CompanyLogoUrl = job.CompanyLogo ?? job.Logo,
                PostedAt = job.Date.HasValue ? job.Date.Value : now,
                FetchedAt = now,
                IsActive = true
            });
        }

        _logger.LogInformation("RemoteOK: fetched {Count} matching listings", results.Count);
        return results;
    }

    private static bool MatchesKeywords(RemoteOkJob job, IReadOnlyList<string> keywords)
    {
        var haystack = string.Join(' ', job.Position, job.Company, string.Join(' ', job.Tags ?? new()))
            .ToLowerInvariant();
        return keywords.Any(k => haystack.Contains(k.ToLowerInvariant()));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private class RemoteOkJob
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("position")] public string? Position { get; set; }
        [JsonPropertyName("company")] public string? Company { get; set; }
        [JsonPropertyName("company_logo")] public string? CompanyLogo { get; set; }
        [JsonPropertyName("logo")] public string? Logo { get; set; }
        [JsonPropertyName("location")] public string? Location { get; set; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("salary_min")] public long? SalaryMin { get; set; }
        [JsonPropertyName("salary_max")] public long? SalaryMax { get; set; }

        [JsonPropertyName("date")]
        [JsonConverter(typeof(FlexibleDateConverter))]
        public DateTimeOffset? Date { get; set; }
    }

    /// <summary>RemoteOK returns dates as ISO-8601 strings; tolerate epoch-seconds too.</summary>
    private class FlexibleDateConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var epoch))
                return DateTimeOffset.FromUnixTimeSeconds(epoch);

            var str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str)) return null;

            if (DateTimeOffset.TryParse(str, out var dt)) return dt;
            if (long.TryParse(str, out var epochStr)) return DateTimeOffset.FromUnixTimeSeconds(epochStr);
            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteStringValue(value.Value.ToString("O"));
            else writer.WriteNullValue();
        }
    }
}
