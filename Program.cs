using System.Text.Json;
using PayBeancoins.HealthChecks;
using PayBeancoins.Messaging;
using PayBeancoins.Models;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<EventPublisher>();

builder.Services.AddSingleton<IReadOnlyList<ConsumerBinding>>(sp =>
{
    var publisher = sp.GetRequiredService<EventPublisher>();
    return new List<ConsumerBinding>
    {
        new("pay.incoming-orders", "order.created", envelope => HandleOrderCreatedAsync(envelope, publisher)),
    };
});
builder.Services.AddHostedService<EventConsumerHostedService>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

var app = builder.Build();

app.UseHttpMetrics();
app.MapMetrics();

app.MapHealthChecks("/healthz/live", new() { Predicate = c => c.Tags.Contains("live") });
app.MapHealthChecks("/healthz/ready", new() { Predicate = c => c.Tags.Contains("ready") });

app.Run();

// Deterministic payment mock (architecture plan Q8): any item named "Rotten Beans"
// (case-insensitive) fails the payment; everything else succeeds.
static Task HandleOrderCreatedAsync(EventEnvelope envelope, EventPublisher publisher)
{
    var payload = envelope.Payload.Deserialize<OrderCreatedPayload>()!;
    var isRotten = payload.Items.Any(i => string.Equals(i.Name, "Rotten Beans", StringComparison.OrdinalIgnoreCase));

    if (isRotten)
    {
        var failedPayload = new PaymentFailedPayload("Simulated failure: Rotten Beans");
        var failedEnvelope = envelope with
        {
            EventId = Guid.NewGuid(),
            EventType = "payment.failed",
            OccurredAt = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToElement(failedPayload),
        };
        publisher.Publish("payment.failed", failedEnvelope);
    }
    else
    {
        var succeededPayload = new PaymentSucceededPayload(payload.TotalAmount);
        var succeededEnvelope = envelope with
        {
            EventId = Guid.NewGuid(),
            EventType = "payment.succeeded",
            OccurredAt = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToElement(succeededPayload),
        };
        publisher.Publish("payment.succeeded", succeededEnvelope);
    }

    return Task.CompletedTask;
}
