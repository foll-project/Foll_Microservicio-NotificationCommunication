using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Interfaces.Realtime;
namespace foll_notifications.NotificationCommunication.Infrastructure.Messaging;
public class TelemetryBuffer(IConfiguration config)
{
    private readonly object _gate = new();
    private readonly Dictionary<long, DeviceTelemetrySnapshot> _pending = new();
    public bool Offer(DeviceTelemetrySnapshot value)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue(value.DeviceId, out var old))
            {
                if (old.LastHeartbeatAtUtc < value.LastHeartbeatAtUtc) _pending[value.DeviceId] = value;
                return true;
            }
            if (_pending.Count >= config.GetValue("Telemetry:Capacity", 1000)) return false;
            _pending[value.DeviceId] = value; return true;
        }
    }
    public IReadOnlyList<DeviceTelemetrySnapshot> Drain(DateTime now)
    {
        lock (_gate)
        {
            var values = _pending.Values.Take(100).ToArray(); foreach (var v in values) _pending.Remove(v.DeviceId);
            return values.Where(v => v.LastHeartbeatAtUtc >= now.AddSeconds(-30)).ToArray();
        }
    }
}
public class TelemetryWorker(TelemetryBuffer buffer, IServiceScopeFactory scopes, ILogger<TelemetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var value in buffer.Drain(DateTime.UtcNow))
            {
                if (value.LastHeartbeatAtUtc < DateTime.UtcNow.AddSeconds(-30)) continue;
                using var scope = scopes.CreateScope();
                try { await scope.ServiceProvider.GetRequiredService<SignalRDeviceTelemetryRealtimePublisher>().PublishTelemetryAsync(value, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning(ex, "Se descartó telemetría efímera de {DeviceId}.", value.DeviceId); }
            }
            try { await Task.Delay(500, ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }
}
