using System.Net;
using System.Text;
using foll_backend.Shared.Infrastructure.Integration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
namespace Foll.Notifications.Tests;
public class TransitionTests
{
    [Theory] [InlineData(false, "local")] [InlineData(true, "remote")]
    public async Task PublicApiProxyIsOptIn(bool remote, string expected)
    {
        using var upstream = HttpAndGatewayTests.Client(request =>
        {
            Assert.Equal("/api/notifications/1/read", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer user", request.Headers.Authorization!.ToString());
            return new(HttpStatusCode.OK) { Content = new StringContent("remote") };
        });
        using var host = Host(remote, upstream); using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/1/read"); request.Headers.Authorization = new("Bearer", "user");
        Assert.Equal(expected, await (await client.SendAsync(request)).Content.ReadAsStringAsync());
    }
    [Fact] public async Task PublicLocationOldUrlIsForwardedWithoutRequiringUserJwt()
    {
        using var upstream = HttpAndGatewayTests.Client(request =>
        {
            Assert.Equal("/api/emergency/public-location/existing-token", request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.NotFound) { Content = new StringContent("expired") };
        });
        using var host = Host(true, upstream);
        Assert.Equal(HttpStatusCode.NotFound, (await host.GetTestClient().GetAsync("/api/emergency/public-location/existing-token")).StatusCode);
    }
    [Fact] public async Task SignalRAndInternalRoutesAreNotPublicHttpProxied()
    {
        using var upstream = HttpAndGatewayTests.Client(_ => throw new Exception("Do not elevate anonymous internal requests with the service key"));
        using var host = Host(true, upstream); using var client = host.GetTestClient();
        Assert.Equal("local", await client.GetStringAsync("/internal/notifications/device-events"));
        Assert.Equal("local", await client.GetStringAsync("/hubs/notifications"));
    }
    [Fact] public async Task RemoteOutageDoesNotReturnStaleLocalNotifications()
    {
        using var upstream = HttpAndGatewayTests.Client(_ => throw new HttpRequestException("offline"));
        using var host = Host(true, upstream);
        Assert.Equal(HttpStatusCode.BadGateway, (await host.GetTestClient().GetAsync("/api/notifications")).StatusCode);
    }
    private static IHost Host(bool remote, HttpClient upstream) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(s =>
    {
        s.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["NotificationCommunication:RemoteEnabled"] = remote.ToString() }).Build());
        s.AddSingleton(new NotificationsServiceClient(upstream));
    }).Configure(app => { app.UseMiddleware<NotificationsProxyMiddleware>(); app.Run(c => c.Response.WriteAsync("local")); })).Start();
}
