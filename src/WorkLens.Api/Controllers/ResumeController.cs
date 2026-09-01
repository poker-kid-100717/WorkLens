using System.Text.Json;
using WorkLens.Core.DTOs;
using WorkLens.Core.Entities;
using WorkLens.Core.Interfaces;
using WorkLens.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace WorkLens.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResumeController : ControllerBase
{
    private const string ResumeProfileName = "Resume Match";

    private readonly IResumeRepository _repo;
    private readonly ISearchProfileRepository _searchProfiles;
    private readonly JobFeedAggregatorService _aggregator;

    public ResumeController(
        IResumeRepository repo,
        ISearchProfileRepository searchProfiles,
        JobFeedAggregatorService aggregator)
    {
        _repo = repo;
        _searchProfiles = searchProfiles;
        _aggregator = aggregator;
    }

    [HttpGet]
    public async Task<ActionResult<List<ResumeDto>>> GetAll(CancellationToken ct)
    {
        var resumes = await _repo.GetAllAsync(ct);
        return Ok(resumes.Select(ToDto).ToList());
    }

    [HttpGet("active")]
    public async Task<ActionResult<ResumeDto>> GetActive(CancellationToken ct)
    {
        var resume = await _repo.GetActiveAsync(ct);
        if (resume is null) return NotFound();
        return Ok(ToDto(resume));
    }

    /// <summary>
    /// Uploads a new resume as plain text. The resume becomes active for match scoring,
    /// and WorkLens derives a lightweight search profile from the resume so future feed
    /// refreshes target relevant roles instead of returning an unfiltered remote-job dump.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ResumeDto>> Upload([FromBody] UploadResumeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RawText))
            return BadRequest("Resume text is empty.");

        var resume = new Resume
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Resume" : request.Name,
            RawText = request.RawText,
            IsActive = true,
            UploadedAt = DateTimeOffset.UtcNow
        };

        await _repo.AddAsync(resume, ct);
        await _repo.SaveChangesAsync(ct);

        await UpsertResumeSearchProfileAsync(request.RawText, ct);

        // Make the new targeting visible immediately rather than waiting for the next
        // background refresh interval.
        await _aggregator.RefreshAllAsync(ct);

        return CreatedAtAction(nameof(GetActive), ToDto(resume));
    }

    private async Task UpsertResumeSearchProfileAsync(string rawText, CancellationToken ct)
    {
        var keywords = ExtractSearchKeywords(rawText);
        var profiles = await _searchProfiles.GetAllAsync(ct);
        var profile = profiles.FirstOrDefault(p =>
            string.Equals(p.Name, ResumeProfileName, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            profile = new SearchProfile
            {
                Name = ResumeProfileName,
                KeywordsJson = JsonSerializer.Serialize(keywords),
                RemoteOnly = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _searchProfiles.AddAsync(profile, ct);
        }
        else
        {
            profile.KeywordsJson = JsonSerializer.Serialize(keywords);
            profile.RemoteOnly = true;
            profile.IsActive = true;
            _searchProfiles.Update(profile);
        }

        await _searchProfiles.SaveChangesAsync(ct);
    }

    private static List<string> ExtractSearchKeywords(string rawText)
    {
        var text = rawText.ToLowerInvariant();
        var keywords = new List<string>();

        static void Add(List<string> target, params string[] values)
        {
            foreach (var value in values)
            {
                if (!target.Contains(value, StringComparer.OrdinalIgnoreCase))
                    target.Add(value);
            }
        }

        if (text.Contains("software engineer"))
            Add(keywords, "software engineer", "software developer");
        if (text.Contains("full stack") || text.Contains("full-stack"))
            Add(keywords, "full stack", "full stack developer", "full stack engineer");
        if (text.Contains(".net") || text.Contains("dotnet"))
            Add(keywords, ".NET", ".NET developer", ".NET engineer");
        if (text.Contains("c#"))
            Add(keywords, "C#", "C# developer");
        if (text.Contains("asp.net"))
            Add(keywords, "ASP.NET");
        if (text.Contains("angular"))
            Add(keywords, "Angular");
        if (text.Contains("azure"))
            Add(keywords, "Azure");
        if (text.Contains("backend") || text.Contains("back-end"))
            Add(keywords, "backend developer", "backend engineer");
        if (text.Contains("architect"))
            Add(keywords, "software architect", "solution architect", "application architect");
        if (text.Contains("lead engineer") || text.Contains("technical lead") || text.Contains("tech lead"))
            Add(keywords, "lead software engineer", "lead developer");

        // Safe fallback for resumes that do not contain one of the recognizable phrases.
        if (keywords.Count == 0)
            Add(keywords, "software engineer", "software developer");

        return keywords.Take(12).ToList();
    }

    private static ResumeDto ToDto(Resume r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        IsActive = r.IsActive,
        UploadedAt = r.UploadedAt,
        CharacterCount = r.RawText.Length
    };
}
