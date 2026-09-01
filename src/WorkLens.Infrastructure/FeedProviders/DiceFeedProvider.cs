using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace WorkLens.Infrastructure.FeedProviders;

/// <summary>
/// Dice's official Job Search MCP server: https://mcp.dice.com/mcp — no API key,
/// no login. It's a Streamable-HTTP MCP endpoint (JSON-RPC over SSE-framed responses),
/// not a plain REST API, so this provider speaks minimal JSON-RPC directly over HTTP
/// rather than pulling in a full MCP client library.
/// </summary>
public class DiceFeedProvider : IJobFeedProvider
{
    private const string Endpoint = "https://mcp.dice.com/mcp";
    private readonly HttpClient _http;
    private readonly ILogger<DiceFeedProvider> _logger;

    public JobSource Source => JobSource.Dice;

    public DiceFeedProvider(HttpClient http, ILogger<DiceFeedProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobListing>> FetchAsync(IReadOnlyList<string> keywords, CancellationToken ct)
    {
        if (keywords.Count == 0)
        {
            _logger.LogInformation("Dice: no keywords configured, skipping (search_jobs requires a keyword).");
            return Array.Empty<JobListing>();
        }

        var now = DateTimeOffset.UtcNow;
        var seen = new Dictionary<string, JobListing>();
        var combinedKeyword = string.Join(' ', keywords.Take(6));

        try
        {
            var results = await SearchAsync(combinedKeyword, ct);
            foreach (var job in results)
            {
                if (job.Guid is null) continue;
                seen[job.Guid] = MapToListing(job, now);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dice search_jobs call failed for keyword '{Keyword}'", combinedKeyword);
        }

        _logger.LogInformation("Dice: fetched {Count} listings", seen.Count);
        return seen.Values.ToList();
    }

    private async Task<List<DiceJob>> SearchAsync(string keyword, CancellationToken ct)
    {
        var requestBody = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "search_jobs",
                arguments = new Dictionary<string, object?>
                {
                    ["keyword"] = keyword,
                    ["jobs_per_page"] = 50,
                    ["sort"] = "datePosted"
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync(ct);
        var json = ExtractJsonPayload(raw);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
            throw new InvalidOperationException($"Dice MCP error: {error}");

        var contentText = root
            .GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "{}";

        var searchResult = JsonSerializer.Deserialize<DiceSearchResult>(contentText, JsonOpts);
        return searchResult?.Data ?? new List<DiceJob>();
    }

    private static string ExtractJsonPayload(string raw)
    {
        var lines = raw.Split('\n');
        var dataLine = lines.FirstOrDefault(l => l.StartsWith("data:", StringComparison.Ordinal));
        if (dataLine is null) return raw;
        return dataLine["data:".Length..].Trim();
    }

    private static JobListing MapToListing(DiceJob job, DateTimeOffset now) => new()
    {
        ExternalId = job.Guid!,
        Source = JobSource.Dice,
        Title = job.Title ?? "Untitled",
        Company = job.CompanyName ?? "Unknown",
        Location = job.JobLocation?.DisplayName,
        IsRemote = job.IsRemote == true || (job.WorkplaceTypes?.Contains("Remote") ?? false),
        SalaryMin = null,
        SalaryMax = null,
        SalaryCurrency = null,
        TagsJson = JsonSerializer.Serialize(new List<string>
        {
            job.EmploymentType ?? string.Empty,
            job.EmployerType ?? string.Empty
        }.Where(s => !string.IsNullOrWhiteSpace(s))),
        // Dice's MCP sometimes returns details-page URLs that respond with 403 outside
        // its normal browser flow. Link to a Dice search for the exact role/company instead,
        // which keeps the result usable without relying on the blocked deep link.
        Url = BuildBrowserSafeSearchUrl(job),
        DescriptionHtml = job.Summary,
        CompanyLogoUrl = job.CompanyLogoUrl,
        PostedAt = job.PostedDate ?? now,
        FetchedAt = now,
        IsActive = true
    };

    private static string BuildBrowserSafeSearchUrl(DiceJob job)
    {
        var query = string.Join(' ', new[] { job.Title, job.CompanyName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return $"https://www.dice.com/jobs?q={Uri.EscapeDataString(query)}";
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private class DiceSearchResult
    {
        [JsonPropertyName("data")] public List<DiceJob>? Data { get; set; }
    }

    private class DiceJob
    {
        [JsonPropertyName("guid")] public string? Guid { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("summary")] public string? Summary { get; set; }
        [JsonPropertyName("postedDate")] public DateTimeOffset? PostedDate { get; set; }
        [JsonPropertyName("jobLocation")] public DiceLocation? JobLocation { get; set; }
        [JsonPropertyName("detailsPageUrl")] public string? DetailsPageUrl { get; set; }
        [JsonPropertyName("companyName")] public string? CompanyName { get; set; }
        [JsonPropertyName("companyLogoUrl")] public string? CompanyLogoUrl { get; set; }
        [JsonPropertyName("employmentType")] public string? EmploymentType { get; set; }
        [JsonPropertyName("employerType")] public string? EmployerType { get; set; }
        [JsonPropertyName("workplaceTypes")] public List<string>? WorkplaceTypes { get; set; }
        [JsonPropertyName("isRemote")] public bool? IsRemote { get; set; }
    }

    private class DiceLocation
    {
        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    }
}
