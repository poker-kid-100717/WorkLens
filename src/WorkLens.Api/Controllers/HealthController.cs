using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkLens.Infrastructure.Persistence;

namespace WorkLens.Api.Controllers;

/// <summary>Lightweight readiness endpoint for local and reverse-proxy health checks.</summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly WorkLensDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(WorkLensDbContext db, ILogger<HealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(ct);
            if (!canConnect)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { status = "degraded", database = false, timestamp = DateTimeOffset.UtcNow });

            return Ok(new { status = "healthy", database = true, timestamp = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database readiness check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { status = "unhealthy", database = false, timestamp = DateTimeOffset.UtcNow });
        }
    }
}
