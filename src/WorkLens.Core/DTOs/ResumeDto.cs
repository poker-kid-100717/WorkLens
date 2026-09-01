namespace WorkLens.Core.DTOs;

public class ResumeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public int CharacterCount { get; set; }
}

public class UploadResumeRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Plain text of the resume. Extract PDF text client-side (PDF.js) before
    /// sending — the backend intentionally has no PDF-parsing dependency.</summary>
    public string RawText { get; set; } = string.Empty;
}

public class JobMatchDto
{
    public int JobListingId { get; set; }
    public int MatchScore { get; set; }
    public List<string> MatchingSkills { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset ScoredAt { get; set; }
}
