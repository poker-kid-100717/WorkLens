using WorkLens.Core.DTOs;
using WorkLens.Core.Entities;
using WorkLens.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WorkLens.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResumeController : ControllerBase
{
    private readonly IResumeRepository _repo;

    public ResumeController(IResumeRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<List<ResumeDto>>> GetAll(CancellationToken ct)
    {
        var resumes = await _repo.GetAllAsync(ct);
        return Ok(resumes.Select(ToDto).ToList());
    }

    [HttpGet("active")]
    public async Task<ActionResult<ResumeDto>> GetActive(CancellationToken ct)
    {
        var resume = await _repo.GetActiveAsync(ct);
        if (resume is null) return NotFound();
        return Ok(ToDto(resume));
    }

    /// <summary>
    /// Uploads a new resume as plain text. Extract text from a PDF client-side (PDF.js)
    /// before calling this — the backend intentionally has no PDF-parsing dependency.
    /// The newly uploaded resume becomes the active one driving match scoring.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ResumeDto>> Upload([FromBody] UploadResumeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RawText))
            return BadRequest("Resume text is empty.");

        var resume = new Resume
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Resume" : request.Name,
            RawText = request.RawText,
            IsActive = true,
            UploadedAt = DateTimeOffset.UtcNow
        };

        await _repo.AddAsync(resume, ct);
        await _repo.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetActive), ToDto(resume));
    }

    private static ResumeDto ToDto(Resume r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        IsActive = r.IsActive,
        UploadedAt = r.UploadedAt,
        CharacterCount = r.RawText.Length
    };
}
