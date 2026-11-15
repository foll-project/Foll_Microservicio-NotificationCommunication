extern alias Emergency;
extern alias Devices;

using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
using foll_notifications.NotificationCommunication.Infrastructure.Messaging;
using foll_notifications.Shared.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using EmergencySender = Emergency::foll_emergency_analytics.EmergencyAnalytics.Infrastructure.Messaging.RabbitMqEmergencyEventPublisher;
using EmergencyEnvelope = Emergency::foll_emergency_analytics.EmergencyAnalytics.Application.Ports.EmergencyEventEnvelope;
using DeviceSender = Devices::foll_device_managment.DeviceManagment.Infrastructure.Messaging.RabbitMqDeviceNotificationPublisher;
using DeviceEnvelope = Devices::foll_device_managment.DeviceManagment.Application.Ports.DeviceEventEnvelope;

namespace Foll.Notifications.Tests;

public class BrokerIntegrationTests
{
    private readonly Inbox _inbox = new();
    private readonly TelemetryBuffer _buffer = new(new ConfigurationBuilder().Build());
    private NotificationEventHandler Handler => new(_inbox, _buffer);

    [Fact]
    public async Task AllThreeQueueConsumersRemainRegisteredIndependently()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["IntegrationConsumers:Enabled"] = "false" }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddIntegrationConsumer<NotificationEventHandler>(config, "invitations", NotificationEventHandler.InvitationTypes);
        services.AddIntegrationConsumer<NotificationEventHandler>(config, "emergency", NotificationEventHandler.EmergencyTypes);
        services.AddIntegrationConsumer<NotificationEventHandler>(config, "devices", NotificationEventHandler.DeviceTypes);
        using var provider = services.BuildServiceProvider();
        var consumers = provider.GetServices<IHostedService>().ToArray();
        Assert.Equal(3, consumers.Length);
        foreach (var consumer in consumers) { await consumer.StartAsync(default); await consumer.StopAsync(default); }
    }

    [Fact]
    public async Task EmergencySenderAndReceiverPreserveIdentityAndActorName()
    {
        var key = Guid.NewGuid();
        var occurred = DateTime.UtcNow;
        var payload = JsonSerializer.SerializeToElement(new EmergencyIncidentClosedIntegrationEvent(key, 1001, 25, 1, "FRONTAL", "Caida", 1,
            EmergencyIncidentStatus.Resolved, occurred, null, 7, "Atendido", "Ana Perez"));
        var source = new EmergencyEnvelope($"{EmergencyAnalyticsEventTypes.IncidentClosedV1}:{key:D}",
            EmergencyAnalyticsEventTypes.IncidentClosedV1, payload, occurred);
        var first = EmergencySender.CreateMessage(source);
        var retry = EmergencySender.CreateMessage(source);
        Assert.Equal(first.EventId, retry.EventId);
        await Handler.HandleAsync(Parse(first.EventType, first.EventId, first.OperationId, first), default);
        var accepted = Assert.Single(_inbox.Messages);
        Assert.Equal(source.EventId, accepted.EventId);
        var value = Assert.IsType<EmergencyIncidentClosedIntegrationEvent>(NotificationEventContract.Parse(accepted));
        Assert.Equal("Ana Perez", value.ClosedByName);
    }

    [Theory]
    [InlineData("created", "Pending")]
    [InlineData("accepted", "Accepted")]
    [InlineData("rejected", "Rejected")]
    public async Task CurrentCareInvitationContractIsAdaptedToRealtime(string kind, string status)
    {
        var e = new CareInvitationChanged(Guid.NewGuid(), Guid.NewGuid(), 41, 25, "Paciente", 7, 8, 1,
            "Familiar", status, DateTime.UtcNow.AddDays(2), kind, DateTime.UtcNow, 1);
        await Handler.HandleAsync(Parse($"care.invitation-{kind}.v1", e.EventId, e.OperationId, e), default);
        var value = Assert.IsType<InvitationRealtimeEvent>(NotificationEventContract.Parse(Assert.Single(_inbox.Messages)));
        Assert.Equal(7, value.TargetUserId);
        Assert.Equal(kind, value.Kind);
        Assert.Equal(status, value.Status);
        Assert.Equal(41, value.InvitationId);
    }

    [Theory]
    [InlineData(DeviceManagmentEventTypes.LowBatteryDetectedV1)]
    [InlineData(DeviceManagmentEventTypes.LowBatteryResolvedV1)]
    [InlineData(DeviceManagmentEventTypes.DeviceDisconnectedV1)]
    [InlineData(DeviceManagmentEventTypes.DeviceReconnectedV1)]
    public async Task CurrentDevicesPublisherMatchesNotificationContract(string type)
    {
        var payload = JsonSerializer.SerializeToElement(new { DeviceId = 1001, PatientId = 25, BatteryLevel = 10,
            ReportedAtUtc = DateTime.UtcNow, DetectedAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow, Cause = 1 });
        var source = new DeviceEnvelope("device-managment:41", 41, type, payload);
        var message = DeviceSender.CreateMessage(source);
        await Handler.HandleAsync(Parse(type, message.EventId, message.OperationId, message), default);
        var accepted = Assert.Single(_inbox.Messages);
        Assert.Equal(41, accepted.OutboxMessageId);
        Assert.Equal("patient:25", NotificationEventContract.Stream(NotificationEventContract.Parse(accepted)));
    }

    [Fact]
    public void BrokerMetadataAndVersionAreValidatedBeforeHandling()
    {
        var e = new CareInvitationChanged(Guid.NewGuid(), Guid.NewGuid(), 41, 25, "Paciente", 7, 8, 1,
            "Familiar", "Pending", DateTime.UtcNow.AddDays(2), "created", DateTime.UtcNow, 1);
        Assert.Throws<InvalidDataException>(() => BrokerEnvelope.Parse("care.invitation-created.v1", Guid.NewGuid().ToString(),
            e.OperationId.ToString(), "care.invitation-created.v1", JsonSerializer.SerializeToUtf8Bytes(e)));
        Assert.Throws<InvalidDataException>(() => Parse("care.invitation-created.v1", e.EventId, e.OperationId, e with { Version = 2 }));
    }

    [Fact]
    public async Task TelemetryUsesEphemeralBufferAndDropsExpiredSnapshot()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var message = new { EventId = id, OperationId = id, OccurredAtUtc = now, EventType = "device-managment.telemetry.v1",
            Payload = new DeviceTelemetrySnapshot(1001, 25, 50, false, true, now), Version = 1 };
        await Handler.HandleAsync(Parse(message.EventType, id, id, message), default);
        Assert.Single(_buffer.Drain(now));
        Assert.Empty(_inbox.Messages);
        await Handler.HandleAsync(Parse(message.EventType, id, id, message with { Payload = message.Payload with { LastHeartbeatAtUtc = now.AddMinutes(-1) } }), default);
        Assert.Empty(_buffer.Drain(now));
    }

    private static BrokerEnvelope Parse<T>(string type, Guid id, Guid operation, T body) =>
        BrokerEnvelope.Parse(type, id.ToString(), operation.ToString(), type, JsonSerializer.SerializeToUtf8Bytes(body));

    private sealed class Inbox : INotificationInbox
    {
        public List<IntegrationEnvelope> Messages { get; } = [];
        public Task<bool> AcceptAsync(IntegrationEnvelope message, CancellationToken ct)
        {
            NotificationEventContract.Parse(message);
            Messages.Add(message);
            return Task.FromResult(true);
        }
    }
}
