using System.Text.Json;
using OrderPulse.Webhooks.Abstractions;
using OrderPulse.Webhooks.Models;

namespace OrderPulse.Webhooks.Mapping;

public sealed class StpOrderCreatedMapper : IWebhookPayloadMapper
{
    public string Key => "STP_ORDER_CREATED";

    public WebhookMappedPayload Map(JsonElement payload)
    {
        var body = new
        {
            caseId = WebhookPayloadReader.GetRequiredString(payload, "orderReference", Key),
            rentalId = WebhookPayloadReader.GetRequiredString(payload, "rentalId", Key),
            result = "success"
        };

        return new WebhookMappedPayload(body);
    }
}

public sealed class ProOrderCancelledMapper : IWebhookPayloadMapper
{
    public string Key => "PRO_ORDER_CANCELLED";

    public WebhookMappedPayload Map(JsonElement payload)
    {
        var body = new
        {
            reference = WebhookPayloadReader.GetRequiredString(payload, "orderReference", Key),
            rentalId = WebhookPayloadReader.GetRequiredString(payload, "rentalId", Key),
            cancelled = true,
            reason = WebhookPayloadReader.GetOptionalString(payload, "reason")
        };

        return new WebhookMappedPayload(body);
    }
}

public sealed class CommerceAccountCreatedMapper : IWebhookPayloadMapper
{
    public string Key => "COMMERCE_ACCOUNT_CREATED";

    public WebhookMappedPayload Map(JsonElement payload)
    {
        var organisationId = WebhookPayloadReader.GetRequiredString(
            payload,
            "organisationId",
            Key);
        var organisationName = WebhookPayloadReader.GetRequiredString(
            payload,
            "organisationName",
            Key);
        var dynamicsAccountId = WebhookPayloadReader.GetRequiredString(
            payload,
            "dynamicsAccountId",
            Key);

        var body = new
        {
            name = organisationName,
            properties = new[]
            {
                new
                {
                    key = "AccountNumber",
                    value = dynamicsAccountId
                }
            }
        };

        var routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["organisationId"] = organisationId
        };

        return new WebhookMappedPayload(body, routeValues);
    }
}

internal static class WebhookPayloadReader
{
    public static string GetRequiredString(
        JsonElement payload,
        string propertyName,
        string mapperKey)
    {
        if (TryGetProperty(payload, propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new WebhookMappingException(
            $"Required payload field '{propertyName}' is missing for mapper '{mapperKey}'.");
    }

    public static string? GetOptionalString(JsonElement payload, string propertyName) =>
        TryGetProperty(payload, propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryGetProperty(
        JsonElement payload,
        string propertyName,
        out JsonElement value)
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in payload.EnumerateObject())
            {
                if (string.Equals(
                    property.Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
