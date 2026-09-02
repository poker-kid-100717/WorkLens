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
    public IActionResult Connect()
    {
        if (!_outlook.IsConfigured)
            return BadRequest("Microsoft Outlook integration is not configured. Set OUTLOOK_CLIENT_ID first.");

        var state = Guid.NewGuid().ToString("N");
        return Redirect(_outlook.BuildAuthorizeUrl(state));
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return Redirect($"{_options.FrontendRedirectUri}?outlook=error&message={Uri.EscapeDataString(error)}");
        if (string.IsNullOrWhiteSpace(code))
            return Redirect($"{_options.FrontendRedirectUri}?outlook=error&message=missing_code");

        try
        {
            await _outlook.ConnectAsync(code, ct);
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

    [HttpGet("communications")]
    public async Task<ActionResult<IReadOnlyList<OutlookCommunication>>> Communications(
        [FromQuery] int? applicationId,
        CancellationToken ct) =>
        Ok(await _outlook.GetCommunicationsAsync(applicationId, ct));
}
