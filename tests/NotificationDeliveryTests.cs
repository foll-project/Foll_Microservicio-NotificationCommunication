using System.Text.Json;
using foll_notifications.ExternalServices.Application.OutboundServices;
using foll_notifications.ExternalServices.Domain.Model;
using foll_notifications.NotificationCommunication.Application.Internal.CommandServices;
using foll_notifications.NotificationCommunication.Application.Internal.Services;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
using foll_notifications.NotificationCommunication.Domain.Model.Commands;
using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.NotificationCommunication.Domain.Model.Enums;
using foll_notifications.NotificationCommunication.Domain.Repositories;
using foll_notifications.Shared.Domain.Repositories;
using Xunit;
namespace Foll.Notifications.Tests;
public class NotificationDeliveryTests
{
    [Fact] public async Task SuccessfulPushIsPersistedAndPublished()
    {
        var f = new Fixture(); f.Tokens.Items.Add(new(7, "token", PushPlatform.Web, "Browser", DateTime.UtcNow));
        await f.Service.Handle(f.Command(NotificationType.LowBattery));
        Assert.Equal(NotificationStatus.Sent, Assert.Single(f.Logs.Items).NotificationStatus); Assert.Equal(1, f.Realtime.Count); Assert.Empty(f.Sms.Requests);
    }
    [Fact] public async Task FallWithoutPushTokenFallsBackToPeruvianSmsAndDeduplicatesContact()
    {
        var f = new Fixture(); f.Recipients.Contacts = [new(25, null, 1, "Contacto", "+51 999999999", "EmergencyContact")];
        await f.Service.Handle(f.Command(NotificationType.FallDetected));
        Assert.Equal("+51999999999", Assert.Single(f.Sms.Requests).PhoneNumber); Assert.Single(f.SmsLogs.Items);
        Assert.Contains("http://test/location", f.Sms.Requests[0].Message);
        Assert.Equal(NotificationStatus.Failed, Assert.Single(f.Logs.Items).NotificationStatus); // push failure != SMS failure
    }
    [Fact] public async Task InvalidTokenIsDeactivatedAndTriggersFallFallback()
    {
        var f = new Fixture(); f.Tokens.Items.Add(new(7, "bad-token", PushPlatform.Web, null, DateTime.UtcNow));
        f.Push.Result = PushNotificationResult.Failed("invalid", 1, ["bad-token"]);
        await f.Service.Handle(f.Command(NotificationType.FallDetected));
        Assert.False(f.Tokens.Items[0].IsActive); Assert.Single(f.Sms.Requests);
    }
    [Fact] public async Task ProviderFailureIsRecordedAsFailureRatherThanReportedSent()
    {
        var f = new Fixture(); f.Tokens.Items.Add(new(7, "token", PushPlatform.Web, null, DateTime.UtcNow)); f.Push.Result = PushNotificationResult.Failed("provider unavailable");
        await f.Service.Handle(f.Command(NotificationType.LowBattery));
        Assert.Equal(NotificationStatus.Failed, Assert.Single(f.Logs.Items).NotificationStatus); Assert.Empty(f.Sms.Requests);
    }
    private sealed class Fixture
    {
        public Logs Logs { get; } = new(); public Tokens Tokens { get; } = new(); public SmsLogs SmsLogs { get; } = new();
        public Recipients Recipients { get; } = new(); public Push Push { get; } = new(); public Sms Sms { get; } = new(); public Realtime Realtime { get; } = new();
        public NotificationCommandService Service => new(Logs, Tokens, Recipients, Realtime, Push, Sms, SmsLogs, new Links(), new Unit());
        public CreateNotificationFromEventCommand Command(NotificationType type) => new(25, 1001, null, type, "Alerta", "Mensaje", JsonSerializer.Serialize(new Dictionary<string,string> { ["incidentKey"] = Guid.NewGuid().ToString() }));
    }
    private class Repo<T> : IBaseRepository<T> where T : class
    {
        public List<T> Items { get; } = []; public Task AddAsync(T value) { Items.Add(value); return Task.CompletedTask; }
        public Task<T?> FindByIdAsync(long id) => Task.FromResult(Items.FirstOrDefault()); public void Update(T value) { }
        public void Remove(T value) => Items.Remove(value); public Task<IEnumerable<T>> ListAsync() => Task.FromResult<IEnumerable<T>>(Items);
    }
    private sealed class Logs : Repo<NotificationLog>, INotificationLogRepository
    {
        public Task<IReadOnlyCollection<NotificationLog>> ListByUserIdAsync(long user) => Task.FromResult<IReadOnlyCollection<NotificationLog>>(Items.Where(n => n.UserId == user).ToArray());
        public Task<NotificationLog?> FindByIdAndUserIdAsync(long id, long user) => Task.FromResult(Items.FirstOrDefault(n => n.NotificationLogId == id && n.UserId == user));
        public Task DeleteByUserIdAsync(long user) { Items.RemoveAll(n => n.UserId == user); return Task.CompletedTask; }
    }
    private sealed class Tokens : Repo<UserPushToken>, IUserPushTokenRepository
    {
        public Task<IReadOnlyCollection<UserPushToken>> ListByUserIdAsync(long user) => Task.FromResult<IReadOnlyCollection<UserPushToken>>(Items.Where(t => t.UserId == user).ToArray());
        public Task<IReadOnlyCollection<UserPushToken>> ListActiveByUserIdAsync(long user) => Task.FromResult<IReadOnlyCollection<UserPushToken>>(Items.Where(t => t.UserId == user && t.IsActive).ToArray());
        public Task<UserPushToken?> FindByIdAndUserIdAsync(long id, long user) => Task.FromResult(Items.FirstOrDefault(t => t.UserPushTokenId == id && t.UserId == user));
        public Task<UserPushToken?> FindByUserIdAndTokenAsync(long user, string token) => Task.FromResult(Items.FirstOrDefault(t => t.UserId == user && t.Token == token));
    }
    private sealed class SmsLogs : Repo<SmsNotificationLog>, ISmsNotificationLogRepository
    {
        public Task<SmsNotificationLog?> FindByIncidentKeyAndPhoneNumberAsync(Guid key, string phone) => Task.FromResult(Items.FirstOrDefault(s => s.IncidentKey == key && s.PhoneNumber == phone));
    }
    private sealed class Recipients : IPatientNotificationAccessService
    {
        public IReadOnlyCollection<PatientSmsRecipientDto> Contacts = [];
        public Task<PatientNotificationRecipientsDto?> GetRecipientsForPatientAsync(long id) => Task.FromResult<PatientNotificationRecipientsDto?>(new(id, [new(id,7,"Ana","999999999")], Contacts));
    }
    private sealed class Push : IPushNotificationSender { public PushNotificationResult Result = PushNotificationResult.Sent("provider-id"); public Task<PushNotificationResult> SendAsync(PushNotificationRequest r, CancellationToken ct = default) => Task.FromResult(Result); }
    private sealed class Sms : ISmsNotificationSender
    {
        public List<SmsNotificationRequest> Requests = []; public Task<SmsNotificationResult> SendAsync(SmsNotificationRequest r, CancellationToken ct = default) { Requests.Add(r); return Task.FromResult(SmsNotificationResult.Sent("sms-id")); }
    }
    private sealed class Realtime : INotificationRealtimePublisher { public int Count; public Task PublishCreatedAsync(NotificationLog log, CancellationToken ct = default) { Count++; return Task.CompletedTask; } }
    private sealed class Unit : IUnitOfWork { public Task CompleteAsync() => Task.CompletedTask; }
    private sealed class Links : IEmergencyLocationLinkService
    {
        public Task<EmergencyLocationLinkResult> CreateLinkAsync(Guid key, long patient, long? device, DateTime now, CancellationToken ct = default) => Task.FromResult(new EmergencyLocationLinkResult("http://test/location", now.AddMinutes(30)));
        public Task<EmergencyLocationLinkResolution?> ResolveAsync(string token, DateTime now, CancellationToken ct = default) => Task.FromResult<EmergencyLocationLinkResolution?>(null);
    }
}
