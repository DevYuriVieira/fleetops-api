namespace FleetOps.Infrastructure.Diagnostics;

using System.Diagnostics.Metrics;

public static class FleetOpsMetrics
{
    public const string MeterName = "FleetOps";
    public const string MeterVersion = "1.0.0";

    public static readonly Meter Meter = new(MeterName, MeterVersion);

    // HTTP Metrics
    public static readonly Counter<long> HttpRequestsTotal = Meter.CreateCounter<long>(
        name: "fleetops.http.requests.total",
        unit: "{requests}",
        description: "Total number of HTTP requests processed by FleetOps API");

    public static readonly Histogram<double> HttpRequestDurationMs = Meter.CreateHistogram<double>(
        name: "fleetops.http.request.duration.ms",
        unit: "ms",
        description: "Duration of HTTP request execution in milliseconds");

    // Outbox Metrics
    public static readonly Counter<long> OutboxPublishedTotal = Meter.CreateCounter<long>(
        name: "fleetops.outbox.messages.published.total",
        unit: "{messages}",
        description: "Total number of outbox messages successfully published to the message broker");

    public static readonly Counter<long> OutboxPublishFailuresTotal = Meter.CreateCounter<long>(
        name: "fleetops.outbox.messages.failed.total",
        unit: "{failures}",
        description: "Total number of failures encountered during outbox message publishing");

    public static readonly Histogram<double> OutboxPublishDurationMs = Meter.CreateHistogram<double>(
        name: "fleetops.outbox.publish.duration.ms",
        unit: "ms",
        description: "Latency of outbox message publishing and broker confirmation in milliseconds");

    // Consumer Metrics
    public static readonly Counter<long> ConsumerProcessedTotal = Meter.CreateCounter<long>(
        name: "fleetops.consumer.messages.processed.total",
        unit: "{messages}",
        description: "Total number of AMQP messages processed by background consumers");

    public static readonly Counter<long> ConsumerFailuresTotal = Meter.CreateCounter<long>(
        name: "fleetops.consumer.messages.failed.total",
        unit: "{failures}",
        description: "Total number of consumer message processing failures");

    public static readonly Counter<long> ConsumerIdempotencyHitsTotal = Meter.CreateCounter<long>(
        name: "fleetops.consumer.idempotency.hits.total",
        unit: "{messages}",
        description: "Total number of duplicate AMQP deliveries intercepted and skipped via idempotency check");

    public static readonly Histogram<double> ConsumerProcessingDurationMs = Meter.CreateHistogram<double>(
        name: "fleetops.consumer.processing.duration.ms",
        unit: "ms",
        description: "Duration of consumer message handling and database commit in milliseconds");
}
