using System.Text.Json;
using WorkLens.Core.DTOs;
using WorkLens.Core.Entities;

namespace WorkLens.Api.Mapping;

public static class MappingExtensions
{
    public static JobListingDto ToDto(this JobListing job)
    {
        List<string> tags;
        try { tags = JsonSerializer.Deserialize<List<string>>(job.TagsJson) ?? new(); }
        catch (JsonException) { tags = new(); }

        return new JobListingDto
        {
            Id = job.Id,
            Source = job.Source.ToString(),
            Title = job.Title,
            Company = job.Company,
            Location = job.Location,
            IsRemote = job.IsRemote,
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            SalaryCurrency = job.SalaryCurrency,
            Tags = tags,
            Url = job.Url,
            CompanyLogoUrl = job.CompanyLogoUrl,
            PostedAt = job.PostedAt,
            FetchedAt = job.FetchedAt,
            IsActive = job.IsActive,
            ApplicationId = job.Application?.Id,
            ApplicationStatus = job.Application?.Status.ToString()
        };
    }

    public static JobApplicationDto ToDto(this JobApplication app)
    {
        var isDue = app.FollowUpAt.HasValue
            && !app.FollowUpDismissed
            && app.FollowUpAt.Value <= DateTimeOffset.UtcNow
            && app.Status is not (Core.Enums.ApplicationStatus.Rejected or Core.Enums.ApplicationStatus.Withdrawn);

        return new JobApplicationDto
        {
            Id = app.Id,
            JobListingId = app.JobListingId,
            ManualEntry = app.ManualEntry,
            Title = app.Title,
            Company = app.Company,
            Location = app.Location,
            Url = app.Url,
            Status = app.Status.ToString(),
            SavedAt = app.SavedAt,
            AppliedAt = app.AppliedAt,
            LastStatusChangeAt = app.LastStatusChangeAt,
            FollowUpAt = app.FollowUpAt,
            FollowUpDismissed = app.FollowUpDismissed,
            FollowUpDue = isDue,
            Notes = app.Notes,
            ContactName = app.ContactName,
            ContactEmail = app.ContactEmail
        };
    }
}
