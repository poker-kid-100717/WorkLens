using WorkLens.Core.Enums;

namespace WorkLens.Core.Entities;

/// <summary>
/// A single job posting ingested from an external feed (RemoteOK, Remotive, Greenhouse)
/// or entered manually. Refreshed on a rolling background schedule.
/// </summary>
public class JobListing
{
    public int Id { get; set; }

    /// <summary>Stable identifier from the upstream source, used for de-duplication on refresh.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public JobSource Source { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsRemote { get; set; }

    public string? SalaryMin { get; set; }
    public string? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }

    /// <summary>Comma-separated tags/skills as reported by the source (stored as JSON text array).</summary>
    public string TagsJson { get; set; } = "[]";

    public string Url { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public string? CompanyLogoUrl { get; set; }

    public DateTimeOffset PostedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>True while the listing still appears in the upstream feed on the most recent refresh.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation
    public JobApplication? Application { get; set; }
}
