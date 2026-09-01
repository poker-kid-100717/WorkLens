using WorkLens.Api.Mapping;
using WorkLens.Core.DTOs;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WorkLens.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IJobApplicationRepository _appRepo;
    private readonly IJobListingRepository _listingRepo;

    public ApplicationsController(IJobApplicationRepository appRepo, IJobListingRepository listingRepo)
    {
        _appRepo = appRepo;
        _listingRepo = listingRepo;
    }

    [HttpGet]
    public async Task<ActionResult<List<JobApplicationDto>>> GetAll([FromQuery] string? status, CancellationToken ct)
    {
        var apps = await _appRepo.GetAllAsync(ct);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ApplicationStatus>(status, true, out var parsed))
            apps = apps.Where(a => a.Status == parsed).ToList();

        return Ok(apps.Select(a => a.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<JobApplicationDto>> GetById(int id, CancellationToken ct)
    {
        var app = await _appRepo.GetByIdAsync(id, ct);
        if (app is null) return NotFound();
        return Ok(app.ToDto());
    }

    /// <summary>Reminders due now: follow-up date has passed, not dismissed, and status is not terminal.</summary>
    [HttpGet("due-followups")]
    public async Task<ActionResult<List<JobApplicationDto>>> GetDueFollowUps(CancellationToken ct)
    {
        var apps = await _appRepo.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var due = apps.Where(a =>
            a.FollowUpAt.HasValue &&
            !a.FollowUpDismissed &&
            a.FollowUpAt.Value <= now &&
            a.Status is not (ApplicationStatus.Rejected or ApplicationStatus.Withdrawn));

        return Ok(due.Select(a => a.ToDto()).ToList());
    }

    /// <summary>Saves a listing from the feed (or creates a manual entry) as a tracked application.</summary>
    [HttpPost]
    public async Task<ActionResult<JobApplicationDto>> Create([FromBody] CreateApplicationRequest request, CancellationToken ct)
    {
        JobListing? listing = null;

        if (request.JobListingId.HasValue)
        {
            listing = await _listingRepo.GetByIdAsync(request.JobListingId.Value, ct);
            if (listing is null) return NotFound("Job listing not found.");

            var existing = await _appRepo.GetByJobListingIdAsync(listing.Id, ct);
            if (existing is not null) return Conflict("This job is already tracked.");
        }
        else if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Company))
        {
            return BadRequest("Title and Company are required for a manual entry.");
        }

        var now = DateTimeOffset.UtcNow;
        var app = new JobApplication
        {
            JobListingId = listing?.Id,
            ManualEntry = listing is null,
            Title = listing?.Title ?? request.Title!,
            Company = listing?.Company ?? request.Company!,
            Location = listing?.Location ?? request.Location,
            Url = listing?.Url ?? request.Url,
            Notes = request.Notes,
            Status = ApplicationStatus.Saved,
            SavedAt = now,
            LastStatusChangeAt = now
        };

        app.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = ApplicationStatus.Saved,
            ToStatus = ApplicationStatus.Saved,
            ChangedAt = now,
            Note = "Saved from feed"
        });

        await _appRepo.AddAsync(app, ct);
        await _appRepo.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = app.Id }, app.ToDto());
    }

    /// <summary>Updates status (pipeline move), reminder, notes, or contact info. Every status change is audited.</summary>
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<JobApplicationDto>> Update(int id, [FromBody] UpdateApplicationRequest request, CancellationToken ct)
    {
        var app = await _appRepo.GetByIdAsync(id, ct);
        if (app is null) return NotFound();

        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<ApplicationStatus>(request.Status, true, out var newStatus))
                return BadRequest($"Unknown status '{request.Status}'.");

            if (newStatus != app.Status)
            {
                app.StatusHistory.Add(new ApplicationStatusHistory
                {
                    FromStatus = app.Status,
                    ToStatus = newStatus,
                    ChangedAt = now,
                    Note = request.StatusChangeNote
                });

                if (newStatus == ApplicationStatus.Applied && app.AppliedAt is null)
                    app.AppliedAt = now;

                app.Status = newStatus;
                app.LastStatusChangeAt = now;
            }
        }

        if (request.FollowUpAt.HasValue)
        {
            app.FollowUpAt = request.FollowUpAt;
            app.FollowUpDismissed = false;
        }

        if (request.FollowUpDismissed.HasValue)
            app.FollowUpDismissed = request.FollowUpDismissed.Value;

        if (request.Notes is not null) app.Notes = request.Notes;
        if (request.ContactName is not null) app.ContactName = request.ContactName;
        if (request.ContactEmail is not null) app.ContactEmail = request.ContactEmail;

        _appRepo.Update(app);
        await _appRepo.SaveChangesAsync(ct);

        return Ok(app.ToDto());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var app = await _appRepo.GetByIdAsync(id, ct);
        if (app is null) return NotFound();

        _appRepo.Remove(app);
        await _appRepo.SaveChangesAsync(ct);
        return NoContent();
    }
}
