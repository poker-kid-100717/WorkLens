using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkLens.Core.Entities;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WorkLens.Infrastructure.Services;

public class OpenAiOptions
{
    /// <summary>Set via JobFeeds:OpenAi:ApiKey / env var JobFeeds__OpenAi__ApiKey. Never commit a real key.</summary>
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
}

/// <summary>
/// Calls the OpenAI Chat Completions API with structured JSON output to score a resume
/// against a job description. Kept in Infrastructure — Core only knows the
/// <see cref="IResumeMatchingService"/> abstraction, so swapping providers later
/// (Azure OpenAI, local model, etc.) means adding a new class here, not touching Core.
/// </summary>
public class OpenAiResumeMatchingService : IResumeMatchingService
{
    private const string Endpoint = "https://api.openai.com/v1/chat/completions";
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiResumeMatchingService> _logger;

    public OpenAiResumeMatchingService(HttpClient http, IOptions<OpenAiOptions> options, ILogger<OpenAiResumeMatchingService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MatchResult> ScoreAsync(Resume resume, JobListing listing, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is not configured (JobFeeds:OpenAi:ApiKey). Resume matching is disabled until it is set.");
        }

        var jobDescription = StripHtml(listing.DescriptionHtml ?? string.Empty);
        var prompt = BuildPrompt(resume.RawText, listing.Title, listing.Company, jobDescription);

        var requestBody = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = "You are a precise technical recruiter assistant. Respond only with valid JSON matching the requested schema." },
                new { role = "user", content = prompt }
            },
            response_format = new { type = "json_object" },
            temperature = 0.2
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI match scoring failed ({Status}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"OpenAI API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";

        var parsed = JsonSerializer.Deserialize<MatchJson>(content, JsonOpts) ?? new MatchJson();

        return new MatchResult(
            Math.Clamp(parsed.Score, 0, 100),
            parsed.MatchingSkills ?? new List<string>(),
            parsed.MissingSkills ?? new List<string>(),
            parsed.Summary ?? string.Empty);
    }

    private static string BuildPrompt(string resumeText, string jobTitle, string company, string jobDescription) => $"""
        Compare this candidate's resume against the job posting below. Return a JSON object with exactly these fields:
        - "score": integer 0-100 rating overall fit (skills, experience level, domain relevance)
        - "matchingSkills": array of up to 8 short strings — skills/requirements from the job the resume clearly satisfies
        - "missingSkills": array of up to 8 short strings — skills/requirements from the job the resume does not show evidence of
        - "summary": one or two sentence plain-English summary of the fit

        JOB TITLE: {jobTitle}
        COMPANY: {company}
        JOB DESCRIPTION:
        {Truncate(jobDescription, 4000)}

        RESUME:
        {Truncate(resumeText, 6000)}
        """;

    private static string Truncate(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[..maxChars];

    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private class MatchJson
    {
        [JsonPropertyName("score")] public int Score { get; set; }
        [JsonPropertyName("matchingSkills")] public List<string>? MatchingSkills { get; set; }
        [JsonPropertyName("missingSkills")] public List<string>? MissingSkills { get; set; }
        [JsonPropertyName("summary")] public string? Summary { get; set; }
    }
}
