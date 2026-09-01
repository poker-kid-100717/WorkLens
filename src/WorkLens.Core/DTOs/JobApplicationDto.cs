namespace WorkLens.Core.DTOs;

public class JobApplicationDto
{
    public int Id { get; set; }
    public int? JobListingId { get; set; }
    public bool ManualEntry { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Url { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public DateTimeOffset? LastStatusChangeAt { get; set; }
    public DateTimeOffset? FollowUpAt { get; set; }
    public bool FollowUpDismissed { get; set; }
    public bool FollowUpDue { get; set; }
    public string? Notes { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
}

public class CreateApplicationRequest
{
    /// <summary>Set this to save/track a listing that came from the live feed (RemoteOK, Remotive, Greenhouse, Dice).</summary>
    public int? JobListingId { get; set; }

    // Required when JobListingId is null — used for "Save from URL" entries, e.g. jobs
    // found on LinkedIn or Indeed, which have no public feed API and can't be auto-ingested.
    // Paste the title/company/link yourself and it tracks exactly like a feed-sourced job.
    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
}

public class UpdateApplicationRequest
{
    public string? Status { get; set; }
    public DateTimeOffset? FollowUpAt { get; set; }
    public bool? FollowUpDismissed { get; set; }
    public string? Notes { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? StatusChangeNote { get; set; }
}
