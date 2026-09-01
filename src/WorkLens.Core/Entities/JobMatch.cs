namespace WorkLens.Core.Entities;

/// <summary>
/// Cached OpenAI-generated match result between a resume and a specific job listing.
/// Keyed by (ResumeId, JobListingId) so re-scoring only happens when the active resume
/// changes or a listing's content changes — not on every feed poll.
/// </summary>
public class JobMatch
{
    public int Id { get; set; }

    public int ResumeId { get; set; }
    public Resume? Resume { get; set; }

    public int JobListingId { get; set; }
    public JobListing? JobListing { get; set; }

    /// <summary>0-100 overall fit score as judged by the model.</summary>
    public int MatchScore { get; set; }

    /// <summary>JSON string array of skills/requirements the resume appears to satisfy.</summary>
    public string MatchingSkillsJson { get; set; } = "[]";

    /// <summary>JSON string array of skills/requirements the resume appears to be missing.</summary>
    public string MissingSkillsJson { get; set; } = "[]";

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset ScoredAt { get; set; }
}
