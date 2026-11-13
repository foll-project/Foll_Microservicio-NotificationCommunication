using System.Net;
using System.Net.Http.Json;
using System.Text;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
using foll_notifications.NotificationCommunication.Infrastructure.Gateways;
using foll_notifications.NotificationCommunication.Infrastructure.Messaging;
using foll_notifications.NotificationCommunication.Interfaces.Internal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace Foll.Notifications.Tests;
public class HttpAndGatewayTests
{
    private const string Key = "test-only-service-key-with-more-than-32-characters";
    [Fact] public async Task AcceptedEventDoesNotRequireProviderOrCareToRunInline()
    {
        var inbox = new Inbox(); using var host = Host(inbox); using var client = host.GetTestClient(); client.DefaultRequestHeaders.Add("X-Foll-Service-Key", Key);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/internal/notifications/emergency-events", ContractAndStateTests.Opening())).StatusCode);
        Assert.Equal(1, inbox.Calls);
        inbox.Duplicate = true;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/internal/notifications/emergency-events", ContractAndStateTests.Opening())).StatusCode);
    }
    [Theory] [InlineData("")] [InlineData("wrong-key")]
    public async Task IncomingEventsRequireServiceKey(string key)
    {
        var inbox = new Inbox(); using var host = Host(inbox); using var client = host.GetTestClient();
        if (key.Length > 0) client.DefaultRequestHeaders.Add("X-Foll-Service-Key", key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/internal/notifications/emergency-events", ContractAndStateTests.Opening())).StatusCode); Assert.Equal(0, inbox.Calls);
    }
    [Fact] public async Task MismatchedProducerRouteIsRejected()
    {
        using var host = Host(new Inbox()); using var client = host.GetTestClient(); client.DefaultRequestHeaders.Add("X-Foll-Service-Key", Key);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/internal/notifications/device-events", ContractAndStateTests.Opening())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notifications")).StatusCode);
    }
    [Fact] public async Task CareGatewayDeduplicatesRecipients()
    {
        using var client = Client(request => new(HttpStatusCode.OK) { Content = JsonContent.Create(new
        {
            patientId = 25, userRecipients = new[] { new { userId = 7, fullName = "Ana", phoneNumber = "999999999" }, new { userId = 7, fullName = "Ana", phoneNumber = "999999999" } },
            emergencyContacts = new[] { new { emergencyContactId = 1, fullName = "Contacto", phoneNumber = "988888888" } }
        }) });
        var result = await new PatientRecipientsGateway(client).GetRecipientsForPatientAsync(25);
        Assert.Single(result!.PushRecipients); Assert.Single(result.SmsRecipients);
    }
    [Fact] public async Task CareOutageIsRetryableRatherThanAnEmptyRecipientList()
    {
        using var client = Client(_ => new(HttpStatusCode.ServiceUnavailable));
        await Assert.ThrowsAsync<HttpRequestException>(() => new PatientRecipientsGateway(client).GetRecipientsForPatientAsync(25));
    }
    [Fact] public async Task Emergency404AndOutageAreDifferent()
    {
        using var absent = Client(_ => new(HttpStatusCode.NotFound)); Assert.Null(await new EmergencyLocationGateway(absent).FindByKeyAsync(Guid.NewGuid(), default));
        using var outage = Client(_ => new(HttpStatusCode.ServiceUnavailable)); await Assert.ThrowsAsync<HttpRequestException>(() => new EmergencyLocationGateway(outage).FindByKeyAsync(Guid.NewGuid(), default));
    }
    private static IHost Host(Inbox inbox) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(s =>
    {
        var c = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["InternalApi:ApiKey"] = Key }).Build();
        s.AddSingleton<IConfiguration>(c); s.AddSingleton<INotificationInbox>(inbox); s.AddSingleton<TelemetryBuffer>();
        s.AddControllers().ConfigureApplicationPartManager(m => { m.ApplicationParts.Clear(); m.ApplicationParts.Add(new AssemblyPart(typeof(IntegrationEventsController).Assembly)); });
        s.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Key)), ValidateAudience = false, ValidateIssuer = false }); s.AddAuthorization();
    }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).Start();
    private sealed class Inbox : INotificationInbox
    {
        public int Calls; public bool Duplicate;
        public Task<bool> AcceptAsync(IntegrationEnvelope e, CancellationToken ct) { NotificationEventContract.Parse(e); Calls++; return Task.FromResult(!Duplicate); }
    }
    internal static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> action) => new(new Handler(action)) { BaseAddress = new Uri("http://test/") };
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(action(request));
    }
}
