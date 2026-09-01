using WorkLens.Core.Entities;

namespace WorkLens.Core.Interfaces;

public record MatchResult(int Score, List<string> MatchingSkills, List<string> MissingSkills, string Summary);

/// <summary>
/// Abstraction over the LLM call that compares a resume against a job description.
/// Implemented in Infrastructure against the OpenAI API so Core stays provider-agnostic.
/// </summary>
public interface IResumeMatchingService
{
    Task<MatchResult> ScoreAsync(Resume resume, JobListing listing, CancellationToken ct);
}
