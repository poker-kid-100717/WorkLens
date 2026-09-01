using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace WorkLens.Infrastructure.FeedProviders;

/// <summary>
/// We Work Remotely's public programming RSS feed. WWR explicitly publishes the feed
/// for third-party job discovery experiences and asks consumers to preserve attribution.
/// </summary>
public class WeWorkRemotelyFeedProvider : IJobFeedProvider
{
    private const string Endpoint = "https://weworkremotely.com/categories/remote-programming-jobs.rss";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static DateTimeOffset _lastFetchedAt = DateTimeOffset.MinValue;
    private static List<WwrJob> _cachedJobs = new();

    private readonly HttpClient _http;
    private readonly ILogger<WeWorkRemotelyFeedProvider> _logger;

    public JobSource Source => JobSource.WeWorkRemotely;

    public WeWorkRemotelyFeedProvider(HttpClient http, ILogger<WeWorkRemotelyFeedProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JobListing>> FetchAsync(IReadOnlyList<string> keywords, CancellationToken ct)
    {
        var jobs = await GetCachedJobsAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var results = jobs
            .Where(IsUsEligible)
            .Where(j => keywords.Count == 0 || MatchesKeywords(j, keywords))
            .Select(j => MapToListing(j, now))
            .ToList();

        _logger.LogInformation("We Work Remotely: returning {Count} matching listings", results.Count);
        return results;
    }

    private async Task<List<WwrJob>> GetCachedJobsAsync(CancellationToken ct)
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
            var xml = await response.Content.ReadAsStringAsync(ct);
            var document = XDocument.Parse(xml);

            _cachedJobs = document
                .Descendants()
                .Where(e => e.Name.LocalName == "item")
                .Select(ParseItem)
                .Where(j => !string.IsNullOrWhiteSpace(j.Link) && !string.IsNullOrWhiteSpace(j.Title))
                .ToList();

            _lastFetchedAt = now;
            return _cachedJobs;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static WwrJob ParseItem(XElement item)
    {
        string? Value(string localName) => item.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value?.Trim();

        var rawTitle = Value("title") ?? string.Empty;
        var (company, title) = SplitCompanyAndTitle(rawTitle);
        var categories = item.Elements()
            .Where(e => e.Name.LocalName == "category")
            .Select(e => e.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new WwrJob
        {
            Title = title,
            Company = company,
            Link = Value("link") ?? Value("guid"),
            Region = Value("region"),
            Description = Value("description"),
            Categories = categories,
            PublishedAt = DateTimeOffset.TryParse(Value("pubDate"), out var published) ? published : null
        };
    }

    private static (string Company, string Title) SplitCompanyAndTitle(string rawTitle)
    {
        var separator = rawTitle.IndexOf(':');
        if (separator > 0 && separator < rawTitle.Length - 1)
            return (rawTitle[..separator].Trim(), rawTitle[(separator + 1)..].Trim());

        return ("We Work Remotely", rawTitle.Trim());
    }

    private static bool IsUsEligible(WwrJob job)
    {
        if (string.IsNullOrWhiteSpace(job.Region)) return true;

        var region = job.Region.ToLowerInvariant();
        if (region.Contains("anywhere") || region.Contains("worldwide") || region.Contains("north america") ||
            region.Contains("united states") || region.Contains("usa") || region.Contains("u.s."))
            return true;

        // Explicit region-only restrictions that clearly exclude a U.S.-based applicant.
        if (region.Contains("europe only") || region.Contains("emea only") || region.Contains("asia only") ||
            region.Contains("apac only") || region.Contains("uk only") || region.Contains("canada only") ||
            region.Contains("latin america only"))
            return false;

        return true;
    }

    private static bool MatchesKeywords(WwrJob job, IReadOnlyList<string> keywords)
    {
        var haystack = string.Join(' ',
            job.Title,
            job.Company,
            job.Region,
            job.Description,
            string.Join(' ', job.Categories))
            .ToLowerInvariant();

        return keywords.Any(k => haystack.Contains(k.ToLowerInvariant()));
    }

    private static JobListing MapToListing(WwrJob job, DateTimeOffset now)
    {
        var (salaryMin, salaryMax) = TryExtractAnnualSalary(job.Title + " " + job.Description);

        return new JobListing
        {
            ExternalId = job.Link!,
            Source = JobSource.WeWorkRemotely,
            Title = job.Title,
            Company = job.Company,
            Location = string.IsNullOrWhiteSpace(job.Region) ? "Remote" : job.Region,
            IsRemote = true,
            SalaryMin = salaryMin,
            SalaryMax = salaryMax,
            SalaryCurrency = salaryMin is not null || salaryMax is not null ? "USD" : null,
            TagsJson = JsonSerializer.Serialize(job.Categories),
            Url = job.Link!,
            DescriptionHtml = job.Description,
            CompanyLogoUrl = null,
            PostedAt = job.PublishedAt ?? now,
            FetchedAt = now,
            IsActive = true
        };
    }

    private static (string? Min, string? Max) TryExtractAnnualSalary(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null);

        var match = Regex.Match(
            text,
            @"\$(?<min>\d{2,3}(?:,\d{3})+|\d{5,6})(?:\s*(?:-|–|—|to)\s*\$?(?<max>\d{2,3}(?:,\d{3})+|\d{5,6}))?",
            RegexOptions.IgnoreCase);

        if (!match.Success) return (null, null);

        static decimal? Parse(string value)
        {
            var normalized = value.Replace(",", string.Empty, StringComparison.Ordinal);
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 50_000
                ? amount
                : null;
        }

        var min = Parse(match.Groups["min"].Value);
        var max = match.Groups["max"].Success ? Parse(match.Groups["max"].Value) : min;
        return (
            min?.ToString("0", CultureInfo.InvariantCulture),
            max?.ToString("0", CultureInfo.InvariantCulture));
    }

    private class WwrJob
    {
        public string Title { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string? Link { get; set; }
        public string? Region { get; set; }
        public string? Description { get; set; }
        public List<string> Categories { get; set; } = new();
        public DateTimeOffset? PublishedAt { get; set; }
    }
}
