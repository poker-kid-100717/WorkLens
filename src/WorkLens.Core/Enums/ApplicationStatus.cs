namespace WorkLens.Core.Enums;

/// <summary>
/// Pipeline stage for a tracked job application.
/// Order reflects typical progression; values are stored as strings in the DB for readability.
/// </summary>
public enum ApplicationStatus
{
    Saved = 0,
    Applied = 1,
    PhoneScreen = 2,
    Interviewing = 3,
    Offer = 4,
    Rejected = 5,
    Withdrawn = 6,
    Ghosted = 7
}
