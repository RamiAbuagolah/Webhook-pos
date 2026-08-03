using Microsoft.Extensions.Options;

namespace OrderPulse.Webhooks.Configuration;

public sealed class WebhookEngineOptions
{
    public const string SectionName = "WebhookEngine";
    public List<WebhookRouteOptions> Routes { get; init; } = [];
    public Dictionary<string, WebhookDestinationOptions> Destinations { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WebhookRouteOptions
{
    public string EventType { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public string DestinationKey { get; init; } = string.Empty;
    public string OperationKey { get; init; } = string.Empty;
    public string MapperKey { get; init; } = string.Empty;
}

public sealed class WebhookDestinationOptions
{
    public string HttpClientName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 30;
    public int RetryCount { get; init; } = 3;
    public Dictionary<string, WebhookOperationOptions> Operations { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class WebhookOperationOptions
{
    public string Path { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public Dictionary<string, string> Headers { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<WebhookAuthPolicyOptions> AuthPolicies { get; init; } = [];
}

public sealed class WebhookAuthPolicyOptions
{
    public string Type { get; init; } = string.Empty;
    public string? HeaderName { get; init; }
    public string? ValueSettingName { get; init; }
    public string? TimestampHeaderName { get; init; }
    public string? SignatureHeaderName { get; init; }
}

public sealed class WebhookEngineOptionsValidator : IValidateOptions<WebhookEngineOptions>
{
    public ValidateOptionsResult Validate(string? name, WebhookEngineOptions options)
    {
        var errors = new List<string>();
        var routeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in options.Routes)
        {
            var routeKey = $"{route.EventType}|{route.Channel}";
            if (!routeKeys.Add(routeKey))
            {
                errors.Add($"Duplicate webhook route: {routeKey}.");
            }

            if (!options.Destinations.TryGetValue(route.DestinationKey, out var destination))
            {
                errors.Add($"Route {routeKey} references missing destination '{route.DestinationKey}'.");
                continue;
            }

            if (!destination.Operations.ContainsKey(route.OperationKey))
            {
                errors.Add($"Route {routeKey} references missing operation '{route.OperationKey}'.");
            }
        }

        foreach (var (key, destination) in options.Destinations)
        {
            if (!Uri.TryCreate(destination.BaseUrl, UriKind.Absolute, out _))
            {
                errors.Add($"Destination '{key}' has an invalid BaseUrl.");
            }

            if (string.IsNullOrWhiteSpace(destination.HttpClientName))
            {
                errors.Add($"Destination '{key}' requires HttpClientName.");
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
