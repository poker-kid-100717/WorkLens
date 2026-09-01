using WorkLens.Core.DTOs;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WorkLens.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IJobApplicationRepository _appRepo;

    public AnalyticsController(IJobApplicationRepository appRepo) => _appRepo = appRepo;

    [HttpGet("summary")]
    public async Task<ActionResult<AnalyticsSummaryDto>> GetSummary(CancellationToken ct)
    {
        var apps = await _appRepo.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var total = apps.Count;
        var applied = apps.Count(a => a.Status != ApplicationStatus.Saved);
        var interviewing = apps.Count(a => a.Status is ApplicationStatus.PhoneScreen or ApplicationStatus.Interviewing or ApplicationStatus.Offer);
        var offers = apps.Count(a => a.Status == ApplicationStatus.Offer);
        var rejected = apps.Count(a => a.Status == ApplicationStatus.Rejected);
        var responded = apps.Count(a => a.Status is not (ApplicationStatus.Saved or ApplicationStatus.Applied or ApplicationStatus.Ghosted));

        var dueFollowUps = apps.Count(a =>
            a.FollowUpAt.HasValue && !a.FollowUpDismissed && a.FollowUpAt.Value <= now &&
            a.Status is not (ApplicationStatus.Rejected or ApplicationStatus.Withdrawn));

        var activeApplications = apps.Count(a => a.Status is not (ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Ghosted));

        var funnelOrder = new[]
        {
            ApplicationStatus.Saved, ApplicationStatus.Applied, ApplicationStatus.PhoneScreen,
            ApplicationStatus.Interviewing, ApplicationStatus.Offer
        };
        var funnel = funnelOrder.Select(s => new FunnelStageDto
        {
            Stage = s.ToString(),
            Count = apps.Count(a => a.Status == s)
        }).ToList();

        // Applications per week over the last 12 weeks, keyed by the Monday of each week (UTC).
        var twelveWeeksAgo = now.AddDays(-84);
        var weeklyBuckets = new List<WeeklyCountDto>();
        for (var weekStart = StartOfWeek(twelveWeeksAgo); weekStart <= StartOfWeek(now); weekStart = weekStart.AddDays(7))
        {
            var weekEnd = weekStart.AddDays(7);
            var count = apps.Count(a => a.SavedAt >= weekStart && a.SavedAt < weekEnd);
            weeklyBuckets.Add(new WeeklyCountDto { WeekStart = weekStart.ToString("yyyy-MM-dd"), Count = count });
        }

        var topCompanies = apps
            .GroupBy(a => a.Company)
            .Select(g => new CompanyCountDto { Company = g.Key, Count = g.Count() })
            .OrderByDescending(c => c.Count)
            .Take(10)
            .ToList();

        return Ok(new AnalyticsSummaryDto
        {
            TotalApplications = total,
            TotalSaved = apps.Count(a => a.Status == ApplicationStatus.Saved),
            TotalApplied = applied,
            TotalInterviewing = interviewing,
            TotalOffers = offers,
            TotalRejected = rejected,
            ResponseRatePercent = applied == 0 ? 0 : Math.Round(responded * 100.0 / applied, 1),
            InterviewRatePercent = applied == 0 ? 0 : Math.Round(interviewing * 100.0 / applied, 1),
            OfferRatePercent = applied == 0 ? 0 : Math.Round(offers * 100.0 / applied, 1),
            ActiveApplications = activeApplications,
            DueFollowUps = dueFollowUps,
            Funnel = funnel,
            ApplicationsPerWeek = weeklyBuckets,
            TopCompanies = topCompanies
        });
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset dt)
    {
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return new DateTimeOffset(dt.Date.AddDays(-diff), TimeSpan.Zero);
    }
}
