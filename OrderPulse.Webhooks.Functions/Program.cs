using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OrderPulse.Webhooks.Abstractions;
using OrderPulse.Webhooks.Configuration;
using OrderPulse.Webhooks.Delivery;
using OrderPulse.Webhooks.Mapping;
using OrderPulse.Webhooks.Publishing;
using OrderPulse.Webhooks.Routing;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, configuration) =>
    {
        configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services
            .AddOptions<WebhookEngineOptions>()
            .Bind(context.Configuration.GetSection(WebhookEngineOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<WebhookEngineOptions>, WebhookEngineOptionsValidator>();

        var webhookOptions = context.Configuration
            .GetSection(WebhookEngineOptions.SectionName)
            .Get<WebhookEngineOptions>()
            ?? throw new InvalidOperationException("WebhookEngine configuration is missing.");

        foreach (var destination in webhookOptions.Destinations.Values)
        {
            services
                .AddHttpClient(destination.HttpClientName, client =>
                {
                    client.BaseAddress = new Uri(destination.BaseUrl);
                })
                .AddStandardResilienceHandler(options =>
                {
                    options.Retry.MaxRetryAttempts = destination.RetryCount;
                    options.Retry.UseJitter = true;
                    options.AttemptTimeout.Timeout =
                        TimeSpan.FromSeconds(destination.TimeoutSeconds);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(
                        destination.TimeoutSeconds * (destination.RetryCount + 1));
                });
        }

        services.AddSingleton(_ =>
        {
            var connectionString = context.Configuration["ServiceBusConnection"]
                ?? throw new InvalidOperationException("ServiceBusConnection is not configured.");

            return new ServiceBusClient(connectionString);
        });

        services.AddSingleton<IWebhookRouteResolver, WebhookRouteResolver>();
        services.AddSingleton<IWebhookPayloadMapperResolver, WebhookPayloadMapperResolver>();
        services.AddSingleton<IWebhookRequestBuilder, WebhookRequestBuilder>();
        services.AddSingleton<IWebhookSender, WebhookSender>();
        services.AddSingleton<IWebhookEngine, WebhookEngine>();
        services.AddSingleton<IWebhookPublisher, ServiceBusWebhookPublisher>();

        services.AddSingleton<IWebhookPayloadMapper, StpOrderCreatedMapper>();
        services.AddSingleton<IWebhookPayloadMapper, ProOrderCancelledMapper>();

        services.AddSingleton<IWebhookAuthPolicyResolver, WebhookAuthPolicyResolver>();
        services.AddSingleton<IWebhookAuthPolicy, ApiKeyWebhookAuthPolicy>();
        services.AddSingleton<IWebhookAuthPolicy, BearerStaticWebhookAuthPolicy>();
        services.AddSingleton<IWebhookAuthPolicy, HmacSha256WebhookAuthPolicy>();
    })
    .Build();

await host.RunAsync();
