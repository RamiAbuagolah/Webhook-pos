using System.Text.Json;
using OrderPulse.Webhooks.Configuration;

namespace OrderPulse.Webhooks.Models;

public sealed record WebhookPublishRequest(
    string EventType,
    string Channel,
    JsonElement Payload,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record DestinationRoute(
    string EventType,
    string Channel,
    string DestinationKey,
    string OperationKey,
    string MapperKey,
    string HttpClientName,
    WebhookDestinationOptions Destination,
    WebhookOperationOptions Operation);

public sealed record WebhookMappedPayload(
    object Body,
    IReadOnlyDictionary<string, string>? RouteValues = null);

public sealed record WebhookRequestResult(
    bool IsSuccess,
    bool IsRetryable,
    int? StatusCode,
    string? FailureCategory,
    string? FailureReason,
    string? ResponseBody = null);

public sealed record WebhookDeliveryResult(
    bool IsSuccess,
    bool IsRetryable,
    int? StatusCode,
    string? FailureCategory,
    string? FailureReason)
{
    public static WebhookDeliveryResult Success(int statusCode) =>
        new(true, false, statusCode, null, null);

    public static WebhookDeliveryResult Failure(
        bool isRetryable,
        int? statusCode,
        string category,
        string reason) =>
        new(false, isRetryable, statusCode, category, reason);
}

public sealed class WebhookRouteNotFoundException(string message) : Exception(message);
public sealed class WebhookConfigurationException(string message) : Exception(message);
public sealed class WebhookMappingException(string message) : Exception(message);
public sealed class WebhookRetryableException(string message) : Exception(message);
