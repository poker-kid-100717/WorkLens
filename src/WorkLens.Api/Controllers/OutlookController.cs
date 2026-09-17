using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WorkLens.Infrastructure.Services;

namespace WorkLens.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OutlookController : ControllerBase
{
    private readonly OutlookCommunicationService _outlook;
    private readonly OutlookOptions _options;
    private readonly ILogger<OutlookController> _logger;

    public OutlookController(
        OutlookCommunicationService outlook,
        IOptions<OutlookOptions> options,
        ILogger<OutlookController> logger)
    {
        _outlook = outlook;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult<OutlookConnectionStatus>> Status(CancellationToken ct) =>
        Ok(await _outlook.GetStatusAsync(ct));

    [HttpGet("connect")]
    public async Task<IActionResult> Connect(CancellationToken ct)
    {
        if (!_outlook.IsConfigured)
            return BadRequest("Microsoft Outlook integration is not configured. Set OUTLOOK_CLIENT_ID first.");

        return Redirect(await _outlook.BeginAuthorizationAsync(ct));
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogWarning("Microsoft authorization returned error {Error}", error);
            return Redirect(BuildFrontendResult("error", "authorization_failed"));
        }

        if (string.IsNullOrWhiteSpace(code))
            return Redirect(BuildFrontendResult("error", "missing_code"));

        if (string.IsNullOrWhiteSpace(state))
            return Redirect(BuildFrontendResult("error", "missing_state"));

        try
        {
            await _outlook.ConnectAsync(code, state, ct);
            await _outlook.SyncAsync(ct);
            return Redirect(BuildFrontendResult("connected"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outlook authorization callback failed");
            return Redirect(BuildFrontendResult("error", "connection_failed"));
        }
    }

    [HttpPost("sync")]
    public async Task<ActionResult<object>> Sync(CancellationToken ct)
    {
        var added = await _outlook.SyncAsync(ct);
        return Ok(new { added });
    }

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect([FromQuery] bool clearCommunications = false, CancellationToken ct = default)
    {
        await _outlook.DisconnectAsync(clearCommunications, ct);
        return NoContent();
    }

    [HttpGet("communications")]
    public async Task<ActionResult<IReadOnlyList<OutlookCommunication>>> Communications(
        [FromQuery] int? applicationId,
        CancellationToken ct) =>
        Ok(await _outlook.GetCommunicationsAsync(applicationId, ct));

    [HttpPatch("communications/{messageId}/application")]
    public async Task<ActionResult<OutlookCommunication>> MatchCommunication(
        string messageId,
        [FromBody] MatchCommunicationRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _outlook.MatchCommunicationAsync(messageId, request.ApplicationId, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("communications/{messageId}/track")]
    public async Task<ActionResult<object>> TrackCommunication(
        string messageId,
        [FromBody] TrackCommunicationRequest request,
        CancellationToken ct)
    {
        try
        {
            var app = await _outlook.TrackCommunicationAsApplicationAsync(
                messageId, request.Title, request.Company, request.Location, ct);
            return Ok(new { applicationId = app.Id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private string BuildFrontendResult(string result, string? message = null)
    {
        var separator = _options.FrontendRedirectUri.Contains('?') ? '&' : '?';
        var url = $"{_options.FrontendRedirectUri}{separator}outlook={Uri.EscapeDataString(result)}";
        return message is null ? url : $"{url}&message={Uri.EscapeDataString(message)}";
    }
}

public sealed class MatchCommunicationRequest
{
    public int? ApplicationId { get; set; }
}

public sealed class TrackCommunicationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
}
