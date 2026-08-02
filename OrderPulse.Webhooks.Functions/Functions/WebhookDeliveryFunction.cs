using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OrderPulse.Webhooks.Abstractions;
using OrderPulse.Webhooks.Models;

namespace OrderPulse.Webhooks.Functions;

public sealed class WebhookDeliveryFunction(
    IWebhookEngine webhookEngine,
    ILogger<WebhookDeliveryFunction> logger)
{
    [Function(nameof(WebhookDeliveryFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger(
            "%WebhookTopic%",
            "%WebhookSubscription%",
            Connection = "ServiceBusConnection",
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = message.Body.ToObjectFromJson<WebhookPublishRequest>()
                ?? throw new JsonException("Webhook message body is empty.");

            Validate(request);

            var result = await webhookEngine.DeliverAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                await messageActions.CompleteMessageAsync(message, cancellationToken);
                return;
            }

            if (result.IsRetryable)
            {
                throw new WebhookRetryableException(
                    result.FailureReason ?? "Webhook delivery failed with a retryable error.");
            }

            await messageActions.DeadLetterMessageAsync(
                message,
                deadLetterReason: result.FailureCategory ?? "PermanentWebhookFailure",
                deadLetterErrorDescription: result.FailureReason ?? "Webhook delivery failed permanently.",
                cancellationToken: cancellationToken);
        }
        catch (WebhookRouteNotFoundException exception)
        {
            await DeadLetterAsync(message, messageActions, "RouteNotFound", exception.Message, cancellationToken);
        }
        catch (WebhookConfigurationException exception)
        {
            await DeadLetterAsync(message, messageActions, "InvalidConfiguration", exception.Message, cancellationToken);
        }
        catch (WebhookMappingException exception)
        {
            await DeadLetterAsync(message, messageActions, "MappingFailed", exception.Message, cancellationToken);
        }
        catch (JsonException exception)
        {
            await DeadLetterAsync(message, messageActions, "InvalidPayload", exception.Message, cancellationToken);
        }
        catch (WebhookRetryableException exception)
        {
            logger.LogWarning(
                exception,
                "Retryable webhook failure. MessageId: {MessageId}, CorrelationId: {CorrelationId}, DeliveryCount: {DeliveryCount}",
                message.MessageId,
                message.CorrelationId,
                message.DeliveryCount);

            throw;
        }
    }

    private static void Validate(WebhookPublishRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            throw new JsonException("EventType is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Channel))
        {
            throw new JsonException("Channel is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            throw new JsonException("CorrelationId is required.");
        }
    }

    private static Task DeadLetterAsync(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        string reason,
        string description,
        CancellationToken cancellationToken) =>
        actions.DeadLetterMessageAsync(
            message,
            deadLetterReason: reason,
            deadLetterErrorDescription: description,
            cancellationToken: cancellationToken);
}
