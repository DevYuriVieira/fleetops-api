# ADR-003 — Direct Integration via RabbitMQ.Client Driver over High-Level Service Bus Frameworks

## Status
Accepted

## Context
FleetOps requires publishing domain events and consuming integration events asynchronously over RabbitMQ. In the .NET ecosystem, two primary integration patterns exist:
1. Adopting high-level service bus abstractions such as MassTransit or NServiceBus.
2. Building targeted messaging infrastructure directly on top of the official `RabbitMQ.Client` driver.

## Problem
High-level service bus frameworks provide broad abstractions (concurrency limiters, sagas, outbox implementations, serialization conventions, and transport abstractions). However, they also introduce significant operational trade-offs:
- **Abstraction Leaks & Hidden Magic:** Frameworks establish their own exchange topologies, exchange types (fanout vs topic), routing keys, and queue names by convention, making it difficult to integrate with polyglot services or external systems expecting standard AMQP structures.
- **Controlled ACK Order & Confirmation Semantics:** In financial and operational systems, precise control over the exact timing of `channel.BasicAckAsync`, Publisher Confirms (`WaitForConfirmsAsync`), channel concurrency, and dead-letter headers is critical.
- **Dependency Bloat:** Heavy frameworks pull substantial dependency trees and opinionated architectural boundaries that can conflict with clean architecture and domain isolation.

## Decision
We implement a direct, minimalistic messaging layer using the official, high-performance async driver **`RabbitMQ.Client` (v7.2.2)**:
1. **Explicit Topology:** `RabbitMqConnection.cs` explicitly declares topic exchanges (`fleetops.events`), dead-letter exchanges (`fleetops.events.dlx`), main processing queues, and dead-letter queues (DLQs) with exact arguments (`x-dead-letter-exchange`, `x-dead-letter-routing-key`, `x-message-ttl`).
2. **Publisher Confirms:** `RabbitMqPublisher.cs` enables broker confirmations (`publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true`) and awaits confirmation before marking outbox operations successful.
3. **Deterministic Dead-Lettering & Retries:** `MaintenanceCompletedConsumer.cs` implements custom error classification:
   - **Poison Messages:** Deserialization failures or invalid domain payloads route directly to the DLQ with `channel.BasicAckAsync` to prevent infinite retry loops.
   - **Transient Failures:** Database timeouts route to TTL-based retry queues (150ms/300ms/450ms in integration tests; 10s/30s/90s in production) before escalating to the DLQ.
4. **Strict Post-Commit ACK:** `channel.BasicAckAsync` is executed strictly **after** the local database transaction containing the idempotency record has committed.

## Alternatives Considered
1. **MassTransit:**
   - *Consideration:* Excellent, mature service bus for .NET.
   - *Rejection:* For the audited bounded context, MassTransit abstracts away the exact topology and ACK boundaries that we need to explicitly audit, demonstrate, and control.
2. **Raw TCP / Sockets:**
   - *Rejection:* Unnecessary reinvention of the AMQP protocol.
3. **Kafka / Confluent.Kafka:**
   - *Rejection:* High operational overhead for partitioned log management; RabbitMQ's queue-based routing, TTL dead-lettering, and selective acknowledgment are superior fits for task and event processing in this problem scope.

## Why This Decision
- Provides full transparency over every AMQP frame, header (`traceparent`), delivery tag, and acknowledgment.
- Eliminates framework overhead and opinionated conventions.
- Allows rigorous adversarial auditing and deterministic testing of failure modes (e.g., consumer crash before ACK, redelivery handling).

## Trade-offs
- **Implementation Responsibility:** The team must explicitly write and maintain queue declaration, connection retry policies, serialization, and consumer dispatch loops rather than relying on framework defaults.
- **Feature Surface:** Advanced features like complex distributed sagas require custom implementation if needed in the future.

## Consequences
- The codebase owns its messaging resilience and topology contract.
- Polyglot services can publish and consume messages using standard AMQP 0-9-1 conventions without framework-specific metadata constraints.

## Risks
- Misconfigured channel handling can cause connection exhaustion. Mitigated by centralizing channel creation in `RabbitMqConnection` and disposing channels deterministically with `await using`.

## Operational Impact
- Transparent inspection of RabbitMQ exchanges and queues via RabbitMQ Management UI without proprietary framework header prefixes.

## Future Evolution
- If saga orchestration across dozens of services becomes necessary, evaluate MassTransit or temporal workflow orchestrators (e.g., Temporal / Elsa).
