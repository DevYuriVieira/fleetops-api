# ADR-012 — Standardized Operational Metrics Architecture with Cardinality Discipline

## Status
Accepted

## Context
Production operations require real-time visibility into throughput, latency, failure rates, and background processing lag. In .NET 10, OpenTelemetry natively integrates with `System.Diagnostics.Metrics`.

## Problem
Two opposing anti-patterns plague observability implementations:
1. **Zero Visibility:** Relying exclusively on logs requires parsing text files, which is slow, expensive, and cannot drive sub-minute automated alerting.
2. **Cardinality Explosion:** Emitting metrics tagged with unbounded dimensions (such as `userId`, `vehicleId`, `messageId`, `traceId`, or dynamic exception messages) causes metric time-series databases (Prometheus, Datadog, CloudWatch) to run out of memory, crash, or incur astronomical billing costs.

## Decision
We implement operational metrics via **`System.Diagnostics.Metrics.Meter("FleetOps")`** adhering to strict cardinality discipline:
1. **Instruments Registered:**
   - `fleetops.http.requests.total`: Counter of API requests.
   - `fleetops.http.request.duration.ms`: Histogram of request latency.
   - `fleetops.outbox.messages.published.total`: Counter of published outbox events.
   - `fleetops.outbox.messages.failed.total`: Counter of failed outbox publication attempts.
   - `fleetops.outbox.publish.duration.ms`: Histogram of outbox publication latency.
   - `fleetops.consumer.messages.processed.total`: Counter of consumer deliveries.
   - `fleetops.consumer.messages.failed.total`: Counter of consumer failures.
   - `fleetops.consumer.idempotency.hits.total`: Counter of duplicate messages skipped.
   - `fleetops.consumer.processing.duration.ms`: Histogram of consumer execution time.
2. **Strict Cardinality Rules:**
   - Allowed dimensions: `event_type` (finite enum string), `status` (`success` / `failure` / `poison_message`), `endpoint` (route template, e.g. `/api/vehicles`), `status_code` (e.g. `200`, `429`, `500`).
   - Strictly prohibited dimensions: `userId`, `vehicleId`, `messageId`, `traceId`, `exceptionMessage`.
3. **Trace Correlation:** High-cardinality debugging is reserved for distributed tracing (`Activity` / W3C Trace Context) and structured logging, where individual IDs are stored in trace spans rather than metric dimensions.

## Alternatives Considered
1. **Custom Middleware with Unbounded URL Paths:**
   - *Rejection:* Tagging metrics with raw URLs like `/api/vehicles/123e4567-e89b...` creates infinite unique time-series.
2. **Third-Party Metric Client SDKs (Prometheus-net, Datadog SDK):**
   - *Rejection:* `System.Diagnostics.Metrics` is the official Microsoft and OpenTelemetry standard, avoiding proprietary vendor lock-in.

## Why This Decision
- Native, zero-allocation metric collection in .NET 10.
- Exportable via standard OpenTelemetry Protocol (OTLP) to Prometheus, Grafana Mimir, Datadog, or AWS CloudWatch.
- Predictable, bounded memory consumption.

## Consequences
- Operations teams can configure alerting on error budget burn and outbox publish failures without risk of telemetry platform crashes.
