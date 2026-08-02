# OrderPulse.Webhooks

A database-free Azure Functions webhook engine for OrderPulse.

## Core rule

```text
EventType + Channel -> exactly one DestinationRoute
```

The caller provides only:

- Event type
- Source channel
- Domain payload
- Correlation ID

The caller does not provide the destination URL, HTTP method, authentication, downstream schema, timeout, or retry policy.

## Flow

```text
OrderPulse Function App
    -> Service Bus topic
    -> WebhookDeliveryFunction
    -> WebhookRouteResolver
    -> WebhookPayloadMapperResolver
    -> Destination-specific mapper
    -> WebhookRequestBuilder
    -> Event-specific authentication policy chain
    -> WebhookSender
    -> Named HttpClient per destination
    -> exactly one downstream endpoint
```

## Included patterns

- Canonical event envelope
- Content-based routing
- Strategy and registry for mappers and authentication
- Message translator / anti-corruption layer
- Builder for `HttpRequestMessage`
- Authentication policy chain
- Named `HttpClient` per destination
- HTTP resilience through `AddStandardResilienceHandler`
- Durable Service Bus retries and dead-lettering

## Configuration

Routes are resolved by `EventType + Channel`.

Destination settings contain shared connection details such as base URL, named client, timeout, and retry count.

Operation settings contain event-specific details such as path, HTTP method, headers, and authentication policies.

Secrets are referenced by environment-variable names and are never stored in `appsettings.json`.

See:

- `OrderPulse.Webhooks/appsettings.json`
- `OrderPulse.Webhooks/local.settings.example.json`

## Example routes

```text
OrderCreated + STP
    -> STP_WEBHOOK
    -> StpOrderCreatedMapper
    -> Bearer authentication

OrderCancelled + PRO
    -> PRO_WEBHOOK
    -> ProOrderCancelledMapper
    -> API key authentication
```

## Local setup

1. Copy `local.settings.example.json` to `local.settings.json`.
2. Add the Service Bus connection string and secrets.
3. Ensure the topic and subscription exist.
4. Run the Function App with Azure Functions Core Tools.

## Adding a new webhook

1. Add one route for the new `EventType + Channel` combination.
2. Add or reuse a destination and operation configuration.
3. Implement and register an `IWebhookPayloadMapper`.
4. Select the operation authentication policies.
5. Publish a canonical `WebhookPublishRequest` through `IWebhookPublisher`.

Generic routing, request building, delivery, retry, and DLQ components should not need event-specific changes.
