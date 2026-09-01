using System.Text.Json;
using WorkLens.Core.Entities;
using WorkLens.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WorkLens.Api.Controllers;

public class SearchProfileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public bool RemoteOnly { get; set; }
    public string? LocationFilter { get; set; }
    public bool IsActive { get; set; }
}

public class SaveSearchProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public bool RemoteOnly { get; set; }
    public string? LocationFilter { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Manages named keyword filters (e.g. ".NET Remote", "Azure Contract") that drive
/// what the background feed refresh searches for across RemoteOK, Remotive, and Greenhouse.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SearchProfilesController : ControllerBase
{
    private readonly ISearchProfileRepository _repo;

    public SearchProfilesController(ISearchProfileRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<List<SearchProfileDto>>> GetAll(CancellationToken ct)
    {
        var profiles = await _repo.GetAllAsync(ct);
        return Ok(profiles.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SearchProfileDto>> Create([FromBody] SaveSearchProfileRequest request, CancellationToken ct)
    {
        var profile = new SearchProfile
        {
            Name = request.Name,
            KeywordsJson = JsonSerializer.Serialize(request.Keywords),
            RemoteOnly = request.RemoteOnly,
            LocationFilter = request.LocationFilter,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repo.AddAsync(profile, ct);
        await _repo.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAll), ToDto(profile));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SearchProfileDto>> Update(int id, [FromBody] SaveSearchProfileRequest request, CancellationToken ct)
    {
        var profile = await _repo.GetByIdAsync(id, ct);
        if (profile is null) return NotFound();

        profile.Name = request.Name;
        profile.KeywordsJson = JsonSerializer.Serialize(request.Keywords);
        profile.RemoteOnly = request.RemoteOnly;
        profile.LocationFilter = request.LocationFilter;
        profile.IsActive = request.IsActive;

        _repo.Update(profile);
        await _repo.SaveChangesAsync(ct);
        return Ok(ToDto(profile));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var profile = await _repo.GetByIdAsync(id, ct);
        if (profile is null) return NotFound();

        _repo.Remove(profile);
        await _repo.SaveChangesAsync(ct);
        return NoContent();
    }

    private static SearchProfileDto ToDto(SearchProfile p)
    {
        List<string> keywords;
        try { keywords = JsonSerializer.Deserialize<List<string>>(p.KeywordsJson) ?? new(); }
        catch (JsonException) { keywords = new(); }

        return new SearchProfileDto
        {
            Id = p.Id,
            Name = p.Name,
            Keywords = keywords,
            RemoteOnly = p.RemoteOnly,
            LocationFilter = p.LocationFilter,
            IsActive = p.IsActive
        };
    }
}
