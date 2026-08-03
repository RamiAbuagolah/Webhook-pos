using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using OrderPulse.Webhooks.Abstractions;
using OrderPulse.Webhooks.Configuration;
using OrderPulse.Webhooks.Models;

namespace OrderPulse.Webhooks.Delivery;

public sealed class WebhookAuthPolicyResolver : IWebhookAuthPolicyResolver
{
    private readonly IReadOnlyDictionary<string, IWebhookAuthPolicy> _policies;

    public WebhookAuthPolicyResolver(IEnumerable<IWebhookAuthPolicy> policies)
    {
        _policies = policies.ToDictionary(
            policy => policy.Type,
            StringComparer.OrdinalIgnoreCase);
    }

    public IWebhookAuthPolicy Resolve(string authType)
    {
        if (_policies.TryGetValue(authType, out var policy))
        {
            return policy;
        }

        throw new WebhookConfigurationException(
            $"No webhook authentication policy is registered for type '{authType}'.");
    }
}

public sealed class ApiKeyWebhookAuthPolicy(IConfiguration configuration) : IWebhookAuthPolicy
{
    public string Type => "ApiKey";

    public Task ApplyAsync(
        HttpRequestMessage request,
        byte[] body,
        WebhookAuthPolicyOptions options,
        CancellationToken cancellationToken = default)
    {
        var headerName = options.HeaderName;
        var value = ReadSecret(configuration, options.ValueSettingName, Type);

        if (string.IsNullOrWhiteSpace(headerName))
        {
            throw new WebhookConfigurationException("ApiKey authentication requires HeaderName.");
        }

        request.Headers.TryAddWithoutValidation(headerName, value);
        return Task.CompletedTask;
    }

    private static string ReadSecret(
        IConfiguration configuration,
        string? settingName,
        string authType)
    {
        if (string.IsNullOrWhiteSpace(settingName))
        {
            throw new WebhookConfigurationException(
                $"{authType} authentication requires ValueSettingName.");
        }

        return configuration[settingName]
            ?? throw new WebhookConfigurationException(
                $"Secret setting '{settingName}' is not configured.");
    }
}

public sealed class BearerStaticWebhookAuthPolicy(IConfiguration configuration)
    : IWebhookAuthPolicy
{
    public string Type => "BearerStatic";

    public Task ApplyAsync(
        HttpRequestMessage request,
        byte[] body,
        WebhookAuthPolicyOptions options,
        CancellationToken cancellationToken = default)
    {
        var token = ReadSecret(configuration, options.ValueSettingName);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return Task.CompletedTask;
    }

    private static string ReadSecret(IConfiguration configuration, string? settingName)
    {
        if (string.IsNullOrWhiteSpace(settingName))
        {
            throw new WebhookConfigurationException(
                "BearerStatic authentication requires ValueSettingName.");
        }

        return configuration[settingName]
            ?? throw new WebhookConfigurationException(
                $"Secret setting '{settingName}' is not configured.");
    }
}

public sealed class HmacSha256WebhookAuthPolicy(IConfiguration configuration)
    : IWebhookAuthPolicy
{
    public string Type => "HmacSha256";

    public Task ApplyAsync(
        HttpRequestMessage request,
        byte[] body,
        WebhookAuthPolicyOptions options,
        CancellationToken cancellationToken = default)
    {
        var secret = ReadSecret(configuration, options.ValueSettingName);
        var timestampHeader = options.TimestampHeaderName ?? "x-webhook-timestamp";
        var signatureHeader = options.SignatureHeaderName ?? "x-webhook-signature";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var payload = Encoding.UTF8.GetBytes($"{timestamp}.")
            .Concat(body)
            .ToArray();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();

        request.Headers.TryAddWithoutValidation(timestampHeader, timestamp);
        request.Headers.TryAddWithoutValidation(signatureHeader, signature);
        return Task.CompletedTask;
    }

    private static string ReadSecret(IConfiguration configuration, string? settingName)
    {
        if (string.IsNullOrWhiteSpace(settingName))
        {
            throw new WebhookConfigurationException(
                "HmacSha256 authentication requires ValueSettingName.");
        }

        return configuration[settingName]
            ?? throw new WebhookConfigurationException(
                $"Secret setting '{settingName}' is not configured.");
    }
}
