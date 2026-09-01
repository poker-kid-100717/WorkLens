using WorkLens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WorkLens.Api.Controllers;

/// <summary>Lightweight liveness/readiness endpoint for on-prem monitoring (e.g. a reverse proxy health check).</summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly WorkLensDbContext _db;

    public HealthController(WorkLensDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(ct);
            return Ok(new { status = canConnect ? "healthy" : "degraded", database = canConnect, timestamp = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", error = ex.Message, timestamp = DateTimeOffset.UtcNow });
        }
    }
}
