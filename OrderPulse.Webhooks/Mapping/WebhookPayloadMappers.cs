using System.Text.Json;
using OrderPulse.Webhooks.Abstractions;
using OrderPulse.Webhooks.Models;

namespace OrderPulse.Webhooks.Mapping;

public sealed class StpOrderCreatedMapper : IWebhookPayloadMapper
{
    public string Key => "STP_ORDER_CREATED";

    public object Map(JsonElement payload)
    {
        return new
        {
            caseId = GetRequiredString(payload, "orderReference"),
            rentalId = GetRequiredString(payload, "rentalId"),
            result = "success"
        };
    }

    private static string GetRequiredString(JsonElement payload, string propertyName)
    {
        if (payload.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new WebhookMappingException(
            $"Required payload field '{propertyName}' is missing for mapper 'STP_ORDER_CREATED'.");
    }
}

public sealed class ProOrderCancelledMapper : IWebhookPayloadMapper
{
    public string Key => "PRO_ORDER_CANCELLED";

    public object Map(JsonElement payload)
    {
        return new
        {
            reference = GetRequiredString(payload, "orderReference"),
            rentalId = GetRequiredString(payload, "rentalId"),
            cancelled = true,
            reason = GetOptionalString(payload, "reason")
        };
    }

    private static string GetRequiredString(JsonElement payload, string propertyName)
    {
        if (payload.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new WebhookMappingException(
            $"Required payload field '{propertyName}' is missing for mapper 'PRO_ORDER_CANCELLED'.");
    }

    private static string? GetOptionalString(JsonElement payload, string propertyName) =>
        payload.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
