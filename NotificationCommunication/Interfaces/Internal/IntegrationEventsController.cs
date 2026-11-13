using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
using foll_notifications.NotificationCommunication.Infrastructure.Messaging;
using foll_notifications.Shared.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
namespace foll_notifications.NotificationCommunication.Interfaces.Internal;
[ApiController, InternalApiKey]
[Route("internal/notifications")]
public class IntegrationEventsController(INotificationInbox inbox, TelemetryBuffer telemetry) : ControllerBase
{
    [HttpPost("emergency-events")]
    public Task<IActionResult> Emergency(IntegrationEnvelope message, CancellationToken ct) => Accept(message, "emergency-analytics.", ct);
    [HttpPost("device-events")]
    public Task<IActionResult> Device(IntegrationEnvelope message, CancellationToken ct) => Accept(message, "device-managment.", ct);
    [HttpPost("invitation-events")]
    public Task<IActionResult> Invitation(IntegrationEnvelope message, CancellationToken ct) => Accept(message, "care.", ct);
    private async Task<IActionResult> Accept(IntegrationEnvelope message, string prefix, CancellationToken ct)
    {
        if (message.Type is null || !message.Type.StartsWith(prefix, StringComparison.Ordinal)) return BadRequest();
        try
        {
            var added = await inbox.AcceptAsync(message, ct);
            return added ? StatusCode(202, new { received = true, processed = false }) : Ok(new { duplicate = true });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (JsonException) { return BadRequest(new { message = "Payload inválido." }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
    [HttpPost("device-telemetry")]
    public IActionResult Telemetry(DeviceTelemetrySnapshot snapshot)
    {
        if (snapshot.DeviceId <= 0 || snapshot.PatientId <= 0 || snapshot.BatteryLevel is < 0 or > 100 || snapshot.LastHeartbeatAtUtc == default) return BadRequest();
        if (snapshot.LastHeartbeatAtUtc < DateTime.UtcNow.AddSeconds(-30)) return NoContent();
        telemetry.Offer(snapshot); return Accepted(); // Ephemeral: overload is intentionally lossy, never a durable alert.
    }
}
