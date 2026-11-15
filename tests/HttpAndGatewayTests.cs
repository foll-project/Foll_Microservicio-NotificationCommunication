using System.Net;
using System.Net.Http.Json;
using System.Text;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
using foll_notifications.NotificationCommunication.Infrastructure.Gateways;
using foll_notifications.NotificationCommunication.Infrastructure.Messaging;

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
    internal static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> action) => new(new Handler(action)) { BaseAddress = new Uri("http://test/") };
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(action(request));
    }
}
