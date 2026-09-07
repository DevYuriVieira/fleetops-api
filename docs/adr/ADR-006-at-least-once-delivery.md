# ADR-006 — Bounded At-Least-Once Delivery Semantics and Broker Acknowledgment Boundaries

## Status
Accepted

## Context
In asynchronous distributed architectures, message delivery semantics define the guarantees provided by producers, brokers, and consumers when transporting events across networks.

Vendors and inexperienced engineering claims frequently mention "exactly-once delivery". We must formalize the exact mathematical and physical guarantees of FleetOps.

## Problem
In any distributed system subject to network partitions, machine crashes, and latency variance (the Two Generals' Problem and FLP Impossibility Result):
- **Exactly-once delivery across the network is physically impossible.**
- A producer may successfully send a message to a broker, but if the network connection drops before the broker's acknowledgment (ACK) reaches the producer, the producer must retransmit to avoid data loss. This retransmission results in duplicate messages in the broker.
- Similarly, a consumer may process a message, but if the process crashes or network disconnects before sending the AMQP ACK to the broker, the broker will redeliver the message to another consumer.

Attempting to promise "exactly-once delivery" creates false confidence and leads to catastrophic data corruption when duplicate deliveries inevitably occur.

## Decision
We formally define and document FleetOps messaging semantics as **Bounded At-Least-Once Delivery**:
1. **Publisher Confirms:** `RabbitMqPublisher` waits for explicit broker confirmation (`WaitForConfirmsAsync`) before marking an outbox message as processed.
2. **Bounded Retries:** The outbox worker retries failed publications up to a configured limit (`maxAttempts`, default: 3). If all attempts fail, the message remains recorded with its error trace for operator intervention.
3. **Escalated Retry with Dead-Letter Queues (DLQ):** The consumer topology implements TTL-based retry queues (10s, 30s, 90s) before routing permanently failed messages to the Dead-Letter Queue (`fleetops.vehicle-maintenance.completed.dlq`).
4. **Separation of Concerns:** We formally distinguish:
   $$\text{Publisher Confirm} \neq \text{Message Delivery} \neq \text{Consumer Business Effect}$$
   - *Publisher Confirm* guarantees broker receipt.
   - *Delivery* is bounded at-least-once.
   - *Business Effect* is guaranteed to execute at most once via consumer idempotency (ADR-007).

## Alternatives Considered
1. **Claiming "Exactly-Once Delivery":**
   - *Rejection:* Technologically dishonest and mathematically unprovable in non-deterministic distributed networks.
2. **At-Most-Once Delivery (Auto-ACK):**
   - *Rejection:* Configuring consumers with `autoAck: true` drops messages if the consumer crashes mid-processing, leading to silent data loss.

## Why This Decision
- Refusal to make unverified or impossible architectural claims.
- Forces downstream consumer design to be defensive, resilient, and idempotent by contract.
- Guarantees zero message loss during transient network partitions or broker restarts within bounded retry limits.

## Trade-offs
- Downstream consumers **must** be implemented with idempotency safeguards; plain non-idempotent consumers will execute duplicate side-effects.
- Retries and dead-lettering add queue overhead and message headers (`x-death`).

## Consequences
- The system achieves reliable event delivery without risking data loss.
- Duplicates are explicitly anticipated and handled gracefully at the application and database boundaries.

## Risks
- Poison messages could cycle indefinitely if retry logic is unbounded. Mitigated by `maxAttempts` and immediate poison detection routing directly to DLQ without retry.

## Operational Impact
- Requires monitoring DLQs (`*.dlq`) for unprocessable messages and tracking outbox messages where `attempts >= maxAttempts`.

## Future Evolution
- Build an administrative dead-letter replay CLI / endpoint allowing operators to re-inject remediated messages from the DLQ back into the main exchange.
