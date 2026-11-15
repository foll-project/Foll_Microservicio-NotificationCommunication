using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AppDbContext = foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration.AppDbContext;

namespace foll_notifications.Shared.Infrastructure.Messaging;

public record BrokerEnvelope(Guid EventId, Guid OperationId, string EventType, string Payload)
{
    public static BrokerEnvelope Parse(string routingKey, string? messageId, string? correlationId, string? type, ReadOnlyMemory<byte> body)
    {
        if (body.Length > 1024 * 1024) throw new InvalidDataException("Evento demasiado grande.");
        var payload = Encoding.UTF8.GetString(body.Span);
        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("EventId", out var id) || !id.TryGetGuid(out var eventId) || eventId == Guid.Empty ||
            !root.TryGetProperty("OperationId", out var op) || !op.TryGetGuid(out var operationId) || operationId == Guid.Empty ||
            !root.TryGetProperty("Version", out var version) || !version.TryGetInt32(out var v) || v != 1 ||
            !root.TryGetProperty("OccurredAtUtc", out var occurred) || !occurred.TryGetDateTime(out var time) || time.Kind != DateTimeKind.Utc ||
            !Guid.TryParse(messageId, out var metadataId) || metadataId != eventId ||
            !Guid.TryParse(correlationId, out var metadataOperation) || metadataOperation != operationId || type != routingKey)
            throw new InvalidDataException("Contrato o metadatos del evento invalidos.");
        return new(eventId, operationId, routingKey, payload);
    }

    public T Read<T>() => JsonSerializer.Deserialize<T>(Payload) ?? throw new InvalidDataException("Evento vacio.");
}

public interface IBrokerMessageHandler
{
    Task HandleAsync(BrokerEnvelope message, CancellationToken ct);
}

// The handler durably accepts events in NotificationInbox before RabbitMQ ACK.
public sealed class InboxProcessor(IBrokerMessageHandler handler)
{
    public async Task<bool> ProcessAsync(BrokerEnvelope message, CancellationToken ct)
    {
        await handler.HandleAsync(message, ct);
        return true;
    }
}
public sealed class BrokerSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "foll.integration";
    public int PublishTimeoutSeconds { get; set; } = 10;
}

public record ConsumerSubscription(BrokerSettings Settings, string Queue, string[] RoutingKeys, bool Enabled);

public static class IntegrationConsumerRegistration
{
    public static IServiceCollection AddIntegrationConsumer<T>(this IServiceCollection services,
        IConfiguration config, string queue, params string[] keys) where T : class, IBrokerMessageHandler
    {
        var settings = config.GetSection("RabbitMq").Get<BrokerSettings>() ?? new();
        var enabled = config.GetValue("IntegrationConsumers:Enabled", true);
        if (enabled && (string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.Password) ||
            settings.Port is < 1 or > 65535 || settings.PublishTimeoutSeconds <= 0 ||
            string.IsNullOrWhiteSpace(settings.HostName) || string.IsNullOrWhiteSpace(settings.ExchangeName)))
            throw new InvalidOperationException("Configure RabbitMq para los consumidores.");
        var subscription = new ConsumerSubscription(settings, queue, keys, enabled);
        services.AddScoped<IBrokerMessageHandler, T>();
        services.AddScoped<InboxProcessor>();
        services.AddSingleton<IHostedService>(sp => new RabbitMqConsumer(subscription, sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<ILogger<RabbitMqConsumer>>()));
        return services;
    }
}

public sealed class RabbitMqConsumer(ConsumerSubscription subscription, IServiceScopeFactory scopes,
    ILogger<RabbitMqConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!subscription.Enabled) return;
        // Yield before connecting so an unavailable broker cannot block HTTP startup on .NET 8.
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ConsumeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Consumidor {Queue} desconectado; reintentando.", subscription.Queue); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var s = subscription.Settings;
        var factory = new ConnectionFactory
        {
            HostName = s.HostName, Port = s.Port, UserName = s.UserName, Password = s.Password,
            VirtualHost = s.VirtualHost, AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(s.PublishTimeoutSeconds),
            ContinuationTimeout = TimeSpan.FromSeconds(s.PublishTimeoutSeconds)
        };
        await using var connection = await factory.CreateConnectionAsync("foll-consumer-" + subscription.Queue, ct);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await channel.ExchangeDeclareAsync(s.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(subscription.Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        var deadQueue = subscription.Queue + ".dead";
        await channel.QueueDeclareAsync(deadQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        foreach (var key in subscription.RoutingKeys)
            await channel.QueueBindAsync(subscription.Queue, s.ExchangeName, key, cancellationToken: ct);
        await channel.BasicQosAsync(0, 1, false, ct);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdownAsync += (_, _) => { finished.TrySetResult(); return Task.CompletedTask; };
        channel.ChannelShutdownAsync += (_, _) => { finished.TrySetResult(); return Task.CompletedTask; };
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                Exception? failure = null;
                for (var attempt = 1; attempt <= 5; attempt++)
                {
                    try
                    {
                        if (!subscription.RoutingKeys.Contains(delivery.RoutingKey))
                            throw new InvalidDataException("Routing key no soportada.");
                        var envelope = BrokerEnvelope.Parse(delivery.RoutingKey, delivery.BasicProperties.MessageId,
                            delivery.BasicProperties.CorrelationId, delivery.BasicProperties.Type, delivery.Body);
                        using var scope = scopes.CreateScope();
                        var processed = await scope.ServiceProvider.GetRequiredService<InboxProcessor>().ProcessAsync(envelope, ct);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
                        logger.LogInformation("Evento {EventId} {Result} en {Queue}.", envelope.EventId,
                            processed ? "aceptado" : "duplicado omitido", subscription.Queue);
                        return;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        failure = ex;
                        if (ex is InvalidDataException or JsonException) break;
                        logger.LogWarning(ex, "Intento {Attempt}/5 fallido en {Queue}.", attempt, subscription.Queue);
                        if (attempt < 5) await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    }
                }
                // Preserve the original event; only ACK after confirmed durable quarantine.
                var properties = new BasicProperties
                {
                    Persistent = true, ContentType = "application/json",
                    MessageId = delivery.BasicProperties.MessageId, CorrelationId = delivery.BasicProperties.CorrelationId,
                    Type = delivery.BasicProperties.Type, Timestamp = delivery.BasicProperties.Timestamp,
                    Headers = new Dictionary<string, object?> { ["failure"] = failure?.GetType().Name ?? "Unknown",
                        ["original-routing-key"] = delivery.RoutingKey }
                };
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(s.PublishTimeoutSeconds));
                await channel.BasicPublishAsync("", deadQueue, true, properties, delivery.Body, timeout.Token);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
                logger.LogError(failure, "Evento {EventId} enviado a {DeadQueue}.", delivery.BasicProperties.MessageId, deadQueue);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { finished.TrySetResult(); }
            catch (Exception ex)
            {
                // Closing the channel requeues unacknowledged deliveries.
                logger.LogError(ex, "No se pudo confirmar ni aislar el mensaje de {Queue}.", subscription.Queue);
                finished.TrySetResult();
            }
        };
        await channel.BasicConsumeAsync(subscription.Queue, false, consumer, ct);
        logger.LogInformation("Consumiendo {Queue}.", subscription.Queue);
        await finished.Task.WaitAsync(ct);
    }
}

