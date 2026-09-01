using System.Text.Json;
using WorkLens.Core.DTOs;
using WorkLens.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace WorkLens.Api.Controllers;

public class ScoreJobsRequest
{
    public List<int> JobListingIds { get; set; } = new();
}

/// <summary>
/// On-demand resume-to-job match scoring. Deliberately not part of the fast feed poll —
/// the frontend calls this for whichever jobs are currently visible, and results are
/// cached in JobMatches so repeat views of the same job don't re-call OpenAI.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MatchController : ControllerBase
{
    private readonly ResumeMatchOrchestrator _orchestrator;

    public MatchController(ResumeMatchOrchestrator orchestrator) => _orchestrator = orchestrator;

    [HttpGet("has-resume")]
    public async Task<ActionResult<bool>> HasActiveResume(CancellationToken ct) =>
        Ok(await _orchestrator.HasActiveResumeAsync(ct));

    [HttpPost("score")]
    public async Task<ActionResult<List<JobMatchDto>>> Score([FromBody] ScoreJobsRequest request, CancellationToken ct)
    {
        if (request.JobListingIds.Count == 0) return Ok(new List<JobMatchDto>());
        if (request.JobListingIds.Count > 50) return BadRequest("Score at most 50 jobs per request.");

        var results = await _orchestrator.ScoreListingsAsync(request.JobListingIds, ct);

        return Ok(results.Values.Select(m => new JobMatchDto
        {
            JobListingId = m.JobListingId,
            MatchScore = m.MatchScore,
            MatchingSkills = Deserialize(m.MatchingSkillsJson),
            MissingSkills = Deserialize(m.MissingSkillsJson),
            Summary = m.Summary,
            ScoredAt = m.ScoredAt
        }).ToList());
    }

    private static List<string> Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
