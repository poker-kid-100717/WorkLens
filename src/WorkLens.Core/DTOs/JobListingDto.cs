namespace WorkLens.Core.DTOs;

public class JobListingDto
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsRemote { get; set; }
    public string? SalaryMin { get; set; }
    public string? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Url { get; set; } = string.Empty;
    public string? CompanyLogoUrl { get; set; }
    public DateTimeOffset PostedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Non-null when this listing has already been saved/tracked by the user.</summary>
    public int? ApplicationId { get; set; }
    public string? ApplicationStatus { get; set; }
}

public class FeedResponseDto
{
    public List<JobListingDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public DateTimeOffset LastRefreshedAt { get; set; }
    public int RefreshIntervalSeconds { get; set; }
    public List<FeedSourceStatusDto> SourceStatuses { get; set; } = new();
}

public class FeedSourceStatusDto
{
    public string Source { get; set; } = string.Empty;
    public bool LastFetchSucceeded { get; set; }
    public DateTimeOffset? LastFetchedAt { get; set; }
    public int ListingCount { get; set; }
    public string? LastError { get; set; }
}
