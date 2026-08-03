# OrderPulse Webhook Engine

The solution is split into two projects:

- `OrderPulse.Webhooks` — reusable class library containing the webhook engine.
- `OrderPulse.Webhooks.Functions` — Azure Functions host containing the Service Bus-triggered Function and application startup.

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
    -> OrderPulse.Webhooks.Functions/WebhookDeliveryFunction
    -> OrderPulse.Webhooks/WebhookRouteResolver
    -> OrderPulse.Webhooks/WebhookPayloadMapperResolver
    -> Destination- and event-specific mapper
    -> WebhookRequestBuilder
    -> Event-specific authentication policy chain
    -> WebhookSender
    -> Named HttpClient per destination
    -> exactly one downstream endpoint
```

## Project responsibilities

### OrderPulse.Webhooks

The class library contains:

- Contracts and models
- Webhook publisher abstraction and Service Bus publisher
- Route resolver
- Payload mapper registry
- Destination/event payload mappers
- Authentication policies
- HTTP request builder
- Dynamic route-value replacement
- Webhook sender
- Delivery result classification
- Configuration models and validation

The class library does not contain:

- Azure Function classes
- `Program.cs`
- `host.json`
- Function App environment settings

### OrderPulse.Webhooks.Functions

The Azure Functions project contains:

- `WebhookDeliveryFunction`
- `Program.cs`
- Dependency injection and named `HttpClient` registration
- Service Bus trigger configuration
- `host.json`
- `appsettings.json`
- `local.settings.example.json`

It references `OrderPulse.Webhooks`.

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

- `OrderPulse.Webhooks.Functions/appsettings.json`
- `OrderPulse.Webhooks.Functions/local.settings.example.json`

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

AccountCreated + COMMERCE
    -> COMMERCE_WEBHOOK
    -> CommerceAccountCreatedMapper
    -> PUT /api/accounts/{organisationId}
    -> Bearer authentication
```

## Account creation webhook

Publish the account creation result using:

```text
EventType = AccountCreated
Channel   = COMMERCE
```

Expected domain payload fields:

```json
{
  "accountId": "account-id",
  "organisationId": "organisation-id",
  "organisationName": "Organisation name",
  "dynamicsAccountId": "C00123456"
}
```

The mapper creates the Commerce request body:

```json
{
  "name": "Organisation name",
  "properties": [
    {
      "key": "AccountNumber",
      "value": "C00123456"
    }
  ]
}
```

The `organisationId` is URL-encoded and replaces `{organisationId}` in the configured operation path. The request is sent as `PUT` using the `Webhook.COMMERCE` named client and the authentication policies configured for the `AccountCreated` operation.

## Local setup

1. Copy `OrderPulse.Webhooks.Functions/local.settings.example.json` to `OrderPulse.Webhooks.Functions/local.settings.json`.
2. Add the Service Bus connection string and secrets.
3. Ensure the topic and subscription exist.
4. Run `OrderPulse.Webhooks.Functions` with Azure Functions Core Tools.

## Adding a new webhook

1. Add one route for the new `EventType + Channel` combination.
2. Add or reuse a destination and operation configuration.
3. Implement and register an `IWebhookPayloadMapper` in the class library.
4. Select the operation authentication policies in the Functions configuration.
5. Publish a canonical `WebhookPublishRequest` through `IWebhookPublisher`.

Generic routing, request building, delivery, retry, and DLQ components should not need event-specific changes.
