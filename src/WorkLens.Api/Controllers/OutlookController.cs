using WorkLens.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace WorkLens.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OutlookController : ControllerBase
{
    private readonly OutlookCommunicationService _outlook;
    private readonly OutlookOptions _options;

    public OutlookController(OutlookCommunicationService outlook, IOptions<OutlookOptions> options)
    {
        _outlook = outlook;
        _options = options.Value;
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
            return Redirect($"{_options.FrontendRedirectUri}?outlook=error&message={Uri.EscapeDataString(error)}");
        if (string.IsNullOrWhiteSpace(code))
            return Redirect($"{_options.FrontendRedirectUri}?outlook=error&message=missing_code");
        if (string.IsNullOrWhiteSpace(state))
            return Redirect($"{_options.FrontendRedirectUri}?outlook=error&message=missing_state");

        try
        {
            await _outlook.ConnectAsync(code, state, ct);
            await _outlook.SyncAsync(ct);
            return Redirect($"{_options.FrontendRedirectUri}?outlook=connected");
        }
        catch (Exception ex)
        {
            return Redirect($"{_options.FrontendRedirectUri}?outlook=error&message={Uri.EscapeDataString(ex.Message)}");
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
