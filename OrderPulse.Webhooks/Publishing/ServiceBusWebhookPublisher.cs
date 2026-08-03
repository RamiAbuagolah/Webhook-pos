using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using OrderPulse.Webhooks.Abstractions;
using OrderPulse.Webhooks.Models;

namespace OrderPulse.Webhooks.Publishing;

public sealed class ServiceBusWebhookPublisher : IWebhookPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;

    public ServiceBusWebhookPublisher(
        ServiceBusClient serviceBusClient,
        IConfiguration configuration)
    {
        var topicName = configuration["WebhookTopic"]
            ?? throw new InvalidOperationException("WebhookTopic is not configured.");

        _sender = serviceBusClient.CreateSender(topicName);
    }

    public async Task PublishAsync<TPayload>(
        string eventType,
        string channel,
        TPayload payload,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var request = new WebhookPublishRequest(
            eventType,
            channel,
            BinaryData.FromObjectAsJson(payload).ToObjectFromJson<System.Text.Json.JsonElement>(),
            correlationId,
            DateTimeOffset.UtcNow);

        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(request))
        {
            Subject = eventType,
            CorrelationId = correlationId,
            MessageId = $"{correlationId}:{eventType}:{channel}"
        };

        message.ApplicationProperties["Channel"] = channel;

        await _sender.SendMessageAsync(message, cancellationToken);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
