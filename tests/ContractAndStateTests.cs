using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.NotificationCommunication.Domain.Model.Enums;
using foll_notifications.NotificationCommunication.Infrastructure.Messaging;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Foll.Notifications.Tests;
public class ContractAndStateTests
{
    internal static IntegrationEnvelope Opening()
    {
        var key = Guid.NewGuid();
        return new($"{EmergencyAnalyticsEventTypes.IncidentOpenedV1}:{key:D}", EmergencyAnalyticsEventTypes.IncidentOpenedV1,
            JsonSerializer.SerializeToElement(new EmergencyIncidentOpenedIntegrationEvent(key, 1001, 25, 1, "FRONTAL", "Caída", 1, DateTime.UtcNow, .9m, -12, -77, "Lima")));
    }
    [Fact] public void EmergencyEventIdentityAndStreamArePreserved()
    {
        var e = Opening(); var value = NotificationEventContract.Parse(e);
        Assert.IsType<EmergencyIncidentOpenedIntegrationEvent>(value); Assert.Equal("patient:25", NotificationEventContract.Stream(value));
        Assert.Throws<ArgumentException>(() => NotificationEventContract.Parse(e with { EventId = "changed-id" }));
    }
    [Theory]
    [InlineData("device-managment.low-battery-detected.v1")]
    [InlineData("device-managment.low-battery-resolved.v1")]
    public void DeviceAlertAcceptsExistingNumericIdContract(string type)
    {
        var e = new IntegrationEnvelope("device-managment:41", type, JsonSerializer.SerializeToElement(new { DeviceId = 1001, PatientId = 25, BatteryLevel = 10, ReportedAtUtc = DateTime.UtcNow }), 41);
        Assert.NotNull(NotificationEventContract.Parse(e));
        Assert.Throws<ArgumentException>(() => NotificationEventContract.Parse(e with { OutboxMessageId = 42 }));
    }
    [Fact] public void UnknownEventsAndDeletedAccountsAreNotAccepted()
    {
        Assert.Throws<ArgumentException>(() => NotificationEventContract.Parse(Opening() with { Type = "iam.account-deleted.v1" }));
        Assert.DoesNotContain(typeof(NotificationLog).Assembly.GetTypes(), t => t.Name.Contains("UserAccountDeleted") || t.Name.Contains("DeleteNotificationsByAccount"));
    }
    [Fact] public void InvitationTargetsExactlyOneUserAndUsesStableIdentity()
    {
        var value = new InvitationRealtimeEvent("accepted", 7, 41, 25, "Paciente", 7, "Ana", 1, "Familiar", "Accepted", "Aceptada", "Mensaje", DateTime.UtcNow);
        var e = new IntegrationEnvelope("care.invitation:accepted:41:7", NotificationEventContract.InvitationType, JsonSerializer.SerializeToElement(value));
        Assert.Equal(value, NotificationEventContract.Parse(e));
        Assert.Throws<ArgumentException>(() => NotificationEventContract.Parse(e with { EventId = "care.invitation:accepted:41:9" }));
    }
    [Fact] public void RetryDoesNotClaimProcessingOrDeliverySuccess()
    {
        var m = new NotificationInboxMessage(); var now = DateTime.UtcNow;
        m.Fail(now, "Care unavailable"); Assert.Null(m.ProcessedAt); Assert.Equal("RetryPending", m.State); Assert.True(m.NextAttemptAt > now);
        m.Complete(now.AddSeconds(3), true); Assert.NotNull(m.ProcessedAt); Assert.Equal("ProcessedWithDeliveryFailures", m.State);
    }
    [Fact] public void PublicLinkExpiresAndRevocationWins()
    {
        var now = DateTime.UtcNow; var link = new EmergencyLocationAccessLink(Guid.NewGuid(), 25, 1001, "hash", now.AddMinutes(30), now);
        Assert.True(link.IsActive(now)); Assert.False(link.IsActive(now.AddMinutes(30))); link.Revoke(now); Assert.False(link.IsActive(now));
    }
    [Fact] public void AcknowledgementAndReadAreIdempotent()
    {
        var now = DateTime.UtcNow; var log = new NotificationLog(7, NotificationType.FallDetected, NotificationChannel.Push, "Caída", "Mensaje", null, null, 25, 1001, now);
        log.MarkRead(now); log.MarkRead(now.AddMinutes(1)); Assert.Equal(now, log.ReadAt);
        log.MarkAcknowledged(now); log.MarkRead(now.AddMinutes(2)); Assert.Equal(NotificationStatus.Acknowledged, log.NotificationStatus);
    }
    [Fact] public void BufferIsBoundedAndKeepsLatestSnapshot()
    {
        var c = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Telemetry:Capacity"] = "1" }).Build();
        var buffer = new TelemetryBuffer(c); var now = DateTime.UtcNow;
        Assert.True(buffer.Offer(new(1001, 25, 50, false, true, now)));
        Assert.True(buffer.Offer(new(1001, 25, 80, false, true, now.AddSeconds(1))));
        Assert.False(buffer.Offer(new(1002, 25, 50, false, true, now)));
        Assert.Equal((short)80, Assert.Single(buffer.Drain(now)).BatteryLevel);
        buffer.Offer(new(1001, 25, 80, false, true, now.AddMinutes(-1))); Assert.Empty(buffer.Drain(now));
    }
    [Fact] public void ModelOwnsOnlyNotificationTables()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=test;Password=test").UseSnakeCaseNamingConvention().Options);
        Assert.Equal(5, db.Model.GetEntityTypes().Count()); Assert.All(db.Model.GetEntityTypes(), e => Assert.Equal("notification", e.GetSchema()));
        Assert.DoesNotContain(typeof(NotificationLog).Assembly.GetReferencedAssemblies(), a => a.Name is "foll-backend" or "Foll-Care" or "Foll-EmergencyAnalytics" or "Foll-DeviceManagment");
    }
}
