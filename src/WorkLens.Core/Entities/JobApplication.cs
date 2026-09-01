using WorkLens.Core.Enums;

namespace WorkLens.Core.Entities;

/// <summary>
/// Represents the user's tracked application against a job listing.
/// Created when the user "saves" or "applies" to a listing from the feed;
/// can also stand alone (ManualEntry = true) for jobs found outside the feed.
/// </summary>
public class JobApplication
{
    public int Id { get; set; }

    /// <summary>Nullable so a manually-entered application isn't required to reference a fetched listing.</summary>
    public int? JobListingId { get; set; }
    public JobListing? JobListing { get; set; }

    public bool ManualEntry { get; set; }

    // Denormalized snapshot fields so the application record survives even if the
    // upstream listing later disappears from the feed or the row is manually created.
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Url { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Saved;

    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public DateTimeOffset? LastStatusChangeAt { get; set; }

    /// <summary>Optional follow-up/reminder date. Surfaced as "due" once it has passed and status is not terminal.</summary>
    public DateTimeOffset? FollowUpAt { get; set; }
    public bool FollowUpDismissed { get; set; }

    public string? Notes { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }

    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
}
