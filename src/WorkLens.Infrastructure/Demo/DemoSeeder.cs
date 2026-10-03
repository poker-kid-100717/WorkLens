using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Infrastructure.Persistence;

namespace WorkLens.Infrastructure.Demo;

/// <summary>
/// Seeds the public demo with fictional search profiles and a tracker pipeline so the
/// Tracker, Analytics, and follow-up reminders have something to show. Companies and
/// contacts are invented; job listings themselves come from the live public feeds.
/// </summary>
public static class DemoSeeder
{
    private sealed record SeedApplication(
        string Title,
        string Company,
        string Location,
        ApplicationStatus Status,
        int SavedDaysAgo,
        int? AppliedDaysAgo,
        int? FollowUpInDays,
        string? Notes,
        ApplicationStatus[] Path);

    private static readonly SeedApplication[] Applications =
    [
        new("Senior .NET Engineer", "Northwind Logistics", "Remote (US)", ApplicationStatus.Interviewing, 26, 24, 2,
            "Panel interview scheduled; system design round on event-driven order processing.",
            [ApplicationStatus.Applied, ApplicationStatus.PhoneScreen, ApplicationStatus.Interviewing]),
        new("Full Stack Developer (Angular / C#)", "Contoso Health", "Remote", ApplicationStatus.PhoneScreen, 18, 16, 0,
            "Recruiter screen went well; waiting on the hiring manager's availability.",
            [ApplicationStatus.Applied, ApplicationStatus.PhoneScreen]),
        new("Software Engineer II, Platform", "Fabrikam Freight", "Hybrid, Dallas TX", ApplicationStatus.Offer, 40, 38, null,
            "Offer received; comparing benefits and remote flexibility.",
            [ApplicationStatus.Applied, ApplicationStatus.PhoneScreen, ApplicationStatus.Interviewing, ApplicationStatus.Offer]),
        new("Backend Engineer, Integrations", "Tailspin Systems", "Remote", ApplicationStatus.Applied, 9, 8, 5,
            "Applied through the careers page; referral requested.",
            [ApplicationStatus.Applied]),
        new("Senior Software Engineer", "Adventure Works Cloud", "Remote (US)", ApplicationStatus.Rejected, 35, 33, null,
            "Declined after the technical screen; feedback asked for more Kubernetes depth.",
            [ApplicationStatus.Applied, ApplicationStatus.PhoneScreen, ApplicationStatus.Rejected]),
        new("Lead .NET Developer", "Wingtip Transport", "Remote", ApplicationStatus.Ghosted, 45, 44, null,
            "No response after two follow-ups.",
            [ApplicationStatus.Applied, ApplicationStatus.Ghosted]),
        new("Staff Engineer, Supply Chain", "Litware Commerce", "Remote", ApplicationStatus.Saved, 3, null, 1,
            "Strong match for logistics background; tailor resume before applying.",
            []),
        new("Application Developer (C#, SQL Server)", "Proseware Analytics", "Remote (US)", ApplicationStatus.Applied, 6, 5, 9,
            null,
            [ApplicationStatus.Applied]),
        new("Cloud Software Engineer (Azure)", "Woodgrove Financial", "Hybrid, Austin TX", ApplicationStatus.Withdrawn, 30, 28, null,
            "Withdrew: role changed to fully on-site.",
            [ApplicationStatus.Applied, ApplicationStatus.Withdrawn]),
        new("Senior Angular Engineer", "Blue Yonder Airlines", "Remote", ApplicationStatus.Applied, 12, 11, -1,
            "Follow up with the recruiter this week.",
            [ApplicationStatus.Applied]),
    ];

    /// <summary>Removes the fictional tracker data so <see cref="SeedAsync"/> can recreate it.</summary>
    public static async Task ResetAsync(WorkLensDbContext db, CancellationToken ct = default)
    {
        await db.JobMatches.ExecuteDeleteAsync(ct);
        await db.ApplicationStatusHistories.ExecuteDeleteAsync(ct);
        await db.JobApplications.ExecuteDeleteAsync(ct);
        await db.Resumes.ExecuteDeleteAsync(ct);
        await db.SearchProfiles.ExecuteDeleteAsync(ct);
    }

    public static async Task SeedAsync(WorkLensDbContext db, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        db.SearchProfiles.AddRange(
            Profile(".NET Remote", ["C#", ".NET", "ASP.NET Core"], remoteOnly: true, now),
            Profile("Full Stack Angular", ["Angular", "TypeScript", "full stack"], remoteOnly: true, now),
            Profile("Logistics Software", ["logistics", "supply chain", "freight"], remoteOnly: false, now));

        foreach (var seed in Applications)
        {
            var savedAt = now.AddDays(-seed.SavedDaysAgo);
            var application = new JobApplication
            {
                ManualEntry = true,
                Title = seed.Title,
                Company = seed.Company,
                Location = seed.Location,
                Url = null,
                Status = seed.Status,
                SavedAt = savedAt,
                AppliedAt = seed.AppliedDaysAgo is { } applied ? now.AddDays(-applied) : null,
                FollowUpAt = seed.FollowUpInDays is { } followUp ? now.AddDays(followUp) : null,
                Notes = seed.Notes,
            };

            // Spread the status changes evenly between the save date and roughly today.
            var previous = ApplicationStatus.Saved;
            for (var i = 0; i < seed.Path.Length; i++)
            {
                var changedAt = savedAt.AddDays((double)seed.SavedDaysAgo * (i + 1) / (seed.Path.Length + 1));
                application.StatusHistory.Add(new ApplicationStatusHistory
                {
                    FromStatus = previous,
                    ToStatus = seed.Path[i],
                    ChangedAt = changedAt,
                });
                application.LastStatusChangeAt = changedAt;
                previous = seed.Path[i];
            }
            application.LastStatusChangeAt ??= savedAt;

            db.JobApplications.Add(application);
        }

        await db.SaveChangesAsync(ct);
    }

    private static SearchProfile Profile(string name, string[] keywords, bool remoteOnly, DateTimeOffset now) => new()
    {
        Name = name,
        KeywordsJson = JsonSerializer.Serialize(keywords),
        RemoteOnly = remoteOnly,
        IsActive = true,
        CreatedAt = now,
    };
}
