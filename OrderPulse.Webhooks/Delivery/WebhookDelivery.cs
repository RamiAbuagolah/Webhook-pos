using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderPulse.Webhooks.Abstractions;
using OrderPulse.Webhooks.Models;

namespace OrderPulse.Webhooks.Delivery;

public sealed class WebhookRequestBuilder(
    IWebhookAuthPolicyResolver authPolicyResolver) : IWebhookRequestBuilder
{
    public async Task<HttpRequestMessage> BuildAsync(
        DestinationRoute route,
        object mappedPayload,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(mappedPayload);
        var request = new HttpRequestMessage(
            new HttpMethod(route.Operation.Method),
            route.Operation.Path)
        {
            Content = new ByteArrayContent(body)
        };

        request.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);

        foreach (var header in route.Operation.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var authOptions in route.Operation.AuthPolicies)
        {
            var policy = authPolicyResolver.Resolve(authOptions.Type);
            await policy.ApplyAsync(request, body, authOptions, cancellationToken);
        }

        return request;
    }
}

public sealed class WebhookSender(
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookSender> logger) : IWebhookSender
{
    public async Task<WebhookDeliveryResult> SendAsync(
        DestinationRoute route,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(route.HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Webhook delivered. EventType: {EventType}, Channel: {Channel}, Destination: {Destination}, StatusCode: {StatusCode}",
                    route.EventType,
                    route.Channel,
                    route.DestinationKey,
                    (int)response.StatusCode);

                return WebhookDeliveryResult.Success((int)response.StatusCode);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var isRetryable = IsRetryable(response.StatusCode);

            logger.LogWarning(
                "Webhook delivery failed. EventType: {EventType}, Channel: {Channel}, Destination: {Destination}, StatusCode: {StatusCode}, IsRetryable: {IsRetryable}, Response: {Response}",
                route.EventType,
                route.Channel,
                route.DestinationKey,
                (int)response.StatusCode,
                isRetryable,
                responseBody);

            return WebhookDeliveryResult.Failure(
                isRetryable,
                (int)response.StatusCode,
                isRetryable ? "TransientHttpFailure" : "PermanentHttpFailure",
                string.IsNullOrWhiteSpace(responseBody)
                    ? $"Webhook endpoint returned HTTP {(int)response.StatusCode}."
                    : responseBody);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WebhookDeliveryResult.Failure(
                true,
                null,
                "Timeout",
                "Webhook request timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Webhook network request failed.");
            return WebhookDeliveryResult.Failure(
                true,
                null,
                "NetworkFailure",
                exception.Message);
        }
    }

    private static bool IsRetryable(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
        || (int)statusCode >= 500;
}

public sealed class WebhookEngine(
    IWebhookRouteResolver routeResolver,
    IWebhookPayloadMapperResolver mapperResolver,
    IWebhookRequestBuilder requestBuilder,
    IWebhookSender sender) : IWebhookEngine
{
    public async Task<WebhookDeliveryResult> DeliverAsync(
        WebhookPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var route = routeResolver.Resolve(request.EventType, request.Channel);
        var mapper = mapperResolver.Resolve(route.MapperKey);
        var mappedPayload = mapper.Map(request.Payload);

        using var httpRequest = await requestBuilder.BuildAsync(
            route,
            mappedPayload,
            request.CorrelationId,
            cancellationToken);

        return await sender.SendAsync(route, httpRequest, cancellationToken);
    }
}
