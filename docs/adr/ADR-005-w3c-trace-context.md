# ADR-005 — End-to-End Distributed Tracing via W3C Trace Context Across Relational Storage and Messaging

## Status
Accepted

## Context
FleetOps API processes operations that traverse multiple asynchronous boundaries:
1. An incoming synchronous HTTP request triggers aggregate mutations and saves domain events.
2. A background worker picks up the outbox message from PostgreSQL at a later time.
3. The worker publishes the event to RabbitMQ.
4. An asynchronous consumer receives the message from RabbitMQ and completes the business workflow.

Without distributed tracing, diagnosing latency bottlenecks, failures, or message loss across these decoupled tiers requires manual correlation of disparate logs.

## Problem
Standard tracing libraries automatically propagate trace contexts across synchronous HTTP calls (via HTTP `traceparent` headers). However, when a transaction writes to a database table (Transactional Outbox) and a background process picks it up seconds or minutes later, the ambient in-memory trace context is lost. 

Similarly, standard AMQP client libraries do not automatically inject or extract W3C headers into broker message properties without explicit middleware.

## Decision
We implement explicit, end-to-end **W3C Trace Context (RFC 00-traceparent)** propagation across both relational storage and RabbitMQ messaging using **OpenTelemetry .NET**:
1. **Source Registration:** We define a custom `ActivitySource` named `"FleetOps"` in `FleetOpsDiagnostics.cs`.
2. **Persistence in Outbox:** During `FleetOpsDbContext.SaveChangesAsync`, the active trace context identifier (`System.Diagnostics.Activity.Current?.Id`) is captured and stored in the `outbox_messages.trace_parent` column.
3. **Producer Span Precedence:** When `OutboxService` processes a message, it parses the stored `TraceParent` into an `ActivityContext` and starts an `ActivityKind.Producer` span named `"OutboxService.ProcessMessage"`. When calling `RabbitMqPublisher`, the worker enforces precedence:
   ```csharp
   traceParent: activity?.Id ?? message.TraceParent
   ```
   ensuring the producer span ID is forwarded as the parent for the message.
4. **AMQP Header Injection:** `RabbitMqPublisher` encodes `traceparent` into `BasicProperties.Headers["traceparent"]`.
5. **Consumer Span Linkage:** `MaintenanceCompletedConsumer` extracts the header, parses it into an `ActivityContext`, and starts an `ActivityKind.Consumer` span named `"MaintenanceCompletedConsumer.Process"`, linking it directly to the producer span.
6. **Runtime Verification:** Automated integration tests in `TracingPropagationTests.cs` utilize `System.Diagnostics.ActivityListener` to verify that `TraceId` is preserved across all three stages and that `ParentSpanId` links each child span correctly.

## Alternatives Considered
1. **Relying Exclusively on Correlation IDs (Guid):**
   - *Rejection:* A custom correlation ID allows grep-searching logs, but does not provide span hierarchy, durations, parent-child causality, or integration with standard tracing backends like Jaeger/Tempo.
2. **Automatic OpenTelemetry Instrumentation for RabbitMQ:**
   - *Rejection:* OpenTelemetry auto-instrumentation packages for RabbitMQ do not bridge the gap between relational outbox tables and AMQP headers. Manual extraction and injection at the outbox boundary is mandatory.

## Why This Decision
- Adheres strictly to the W3C Trace Context recommendation, ensuring compatibility with standard observability tools (Jaeger, OpenTelemetry Collector, Datadog, Grafana Tempo).
- Provides a unified distributed trace showing the entire lifecycle: HTTP command &rarr; Database commit &rarr; Outbox publishing &rarr; Consumer execution.

## Trade-offs
- **Implementation Effort:** Requires explicit manual code to serialize, persist, deserialize, and inject trace headers at each boundary.
- **Storage Overhead:** Adds a nullable `varchar` column (`trace_parent`) to the `outbox_messages` table.

## Consequences
- Every domain event published through the outbox carries complete distributed lineage.
- Developers and operators can inspect a single Trace ID in Jaeger to trace an asynchronous operation from REST request to final consumer database commit.

## Risks
- If an activity fails to start (e.g., when sampling is disabled or no listener is attached), `activity?.Id` evaluates to null. The fallback `?? message.TraceParent` safely preserves the original trace context.

## Operational Impact
- Traces are exported via OTLP to Jaeger (configured in `compose.yaml`), enabling real-time visual inspection in local development and production.

## Future Evolution
- Integrate OpenTelemetry Metrics (`Meter`) alongside Tracing to emit Prometheus-compatible operational counters for outbox backlog and consumer processing rates.
