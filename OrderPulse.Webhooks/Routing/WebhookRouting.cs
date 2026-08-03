using Microsoft.Extensions.Options;
using OrderPulse.Webhooks.Abstractions;
using OrderPulse.Webhooks.Configuration;
using OrderPulse.Webhooks.Models;

namespace OrderPulse.Webhooks.Routing;

public sealed class WebhookRouteResolver : IWebhookRouteResolver
{
    private readonly IReadOnlyDictionary<string, WebhookRouteOptions> _routes;
    private readonly WebhookEngineOptions _options;

    public WebhookRouteResolver(IOptions<WebhookEngineOptions> options)
    {
        _options = options.Value;
        _routes = _options.Routes.ToDictionary(
            route => BuildKey(route.EventType, route.Channel),
            StringComparer.OrdinalIgnoreCase);
    }

    public DestinationRoute Resolve(string eventType, string channel)
    {
        var key = BuildKey(eventType, channel);
        if (!_routes.TryGetValue(key, out var route))
        {
            throw new WebhookRouteNotFoundException(
                $"No webhook route is configured for EventType '{eventType}' and Channel '{channel}'.");
        }

        if (!_options.Destinations.TryGetValue(route.DestinationKey, out var destination))
        {
            throw new WebhookConfigurationException(
                $"Webhook destination '{route.DestinationKey}' is not configured.");
        }

        if (!destination.Operations.TryGetValue(route.OperationKey, out var operation))
        {
            throw new WebhookConfigurationException(
                $"Webhook operation '{route.OperationKey}' is not configured for destination '{route.DestinationKey}'.");
        }

        return new DestinationRoute(
            route.EventType,
            route.Channel,
            route.DestinationKey,
            route.OperationKey,
            route.MapperKey,
            destination.HttpClientName,
            destination,
            operation);
    }

    private static string BuildKey(string eventType, string channel) =>
        $"{eventType.Trim()}|{channel.Trim()}";
}

public sealed class WebhookPayloadMapperResolver : IWebhookPayloadMapperResolver
{
    private readonly IReadOnlyDictionary<string, IWebhookPayloadMapper> _mappers;

    public WebhookPayloadMapperResolver(IEnumerable<IWebhookPayloadMapper> mappers)
    {
        _mappers = mappers.ToDictionary(
            mapper => mapper.Key,
            StringComparer.OrdinalIgnoreCase);
    }

    public IWebhookPayloadMapper Resolve(string mapperKey)
    {
        if (_mappers.TryGetValue(mapperKey, out var mapper))
        {
            return mapper;
        }

        throw new WebhookConfigurationException(
            $"No webhook payload mapper is registered for key '{mapperKey}'.");
    }
}
