using System.Text.Json;
using OrderPulse.Webhooks.Configuration;
using OrderPulse.Webhooks.Models;

namespace OrderPulse.Webhooks.Abstractions;

public interface IWebhookPublisher
{
    Task PublishAsync<TPayload>(
        string eventType,
        string channel,
        TPayload payload,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public interface IWebhookEngine
{
    Task<WebhookDeliveryResult> DeliverAsync(
        WebhookPublishRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWebhookRouteResolver
{
    DestinationRoute Resolve(string eventType, string channel);
}

public interface IWebhookPayloadMapper
{
    string Key { get; }
    WebhookMappedPayload Map(JsonElement payload);
}

public interface IWebhookPayloadMapperResolver
{
    IWebhookPayloadMapper Resolve(string mapperKey);
}

public interface IWebhookRequestBuilder
{
    Task<HttpRequestMessage> BuildAsync(
        DestinationRoute route,
        WebhookMappedPayload mappedPayload,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public interface IWebhookAuthPolicy
{
    string Type { get; }

    Task ApplyAsync(
        HttpRequestMessage request,
        byte[] body,
        WebhookAuthPolicyOptions options,
        CancellationToken cancellationToken = default);
}

public interface IWebhookAuthPolicyResolver
{
    IWebhookAuthPolicy Resolve(string authType);
}

public interface IWebhookSender
{
    Task<WebhookDeliveryResult> SendAsync(
        DestinationRoute route,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default);
}
