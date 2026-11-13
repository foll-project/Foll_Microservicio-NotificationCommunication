using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.Internal.Services;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace foll_notifications.NotificationCommunication.Interfaces.REST;
[ApiController, AllowAnonymous]
[Route("api/emergency/public-location")]
public class PublicEmergencyLocationController(IEmergencyLocationLinkService links, IEmergencyLocationGateway emergencies) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> GetByToken(string token, CancellationToken ct)
    {
        var resolution = await links.ResolveAsync(token, DateTime.UtcNow, ct);
        if (resolution is null) return NotFound(new { message = "El enlace no existe o ya expiro." });
        try
        {
            var remote = await emergencies.FindByKeyAsync(resolution.Link.IncidentKey, ct);
            if (remote is null || remote.Value.GetProperty("patientId").GetInt64() != resolution.Link.PatientId)
                return NotFound(new { message = "Incidente no encontrado." });
            var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(remote.Value.GetRawText())!;
            result["expiresAt"] = JsonSerializer.SerializeToElement(resolution.Link.ExpiresAt);
            return Ok(result);
        }
        catch (HttpRequestException) { return StatusCode(503, new { message = "La ubicación no está disponible temporalmente." }); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return StatusCode(503); }
    }
}
