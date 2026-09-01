using WorkLens.Core.Enums;

namespace WorkLens.Core.Entities;

/// <summary>
/// Immutable audit trail entry recorded every time a JobApplication's status changes.
/// Powers the analytics funnel and time-in-stage calculations.
/// </summary>
public class ApplicationStatusHistory
{
    public int Id { get; set; }

    public int JobApplicationId { get; set; }
    public JobApplication? JobApplication { get; set; }

    public ApplicationStatus FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Note { get; set; }
}
