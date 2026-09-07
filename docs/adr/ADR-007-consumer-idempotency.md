# ADR-007 — Relational Consumer Idempotency and Single Business Effect Enforcement

## Status
Accepted

## Context
Under Bounded At-Least-Once Delivery semantics (ADR-006), consumers in FleetOps API will occasionally receive duplicate deliveries of the same message (e.g., due to network timeouts during broker ACK transmission, consumer crashes, or outbox republishing).

The consumer audited in FleetOps is `MaintenanceCompletedConsumer`, which processes `MaintenanceCompletedDomainEvent` and records operational completion records.

## Problem
If an event consumer executes non-idempotent business logic upon receiving duplicate messages, the system suffers severe data corruption anomalies:
- Accounting records are duplicated.
- Entity state transitions occur multiple times or fail validation.
- Downstream events are emitted multiple times, triggering cascading duplicate actions across the ecosystem.

## Decision
We enforce the guarantee: **Single Business Effect within the Audited Transactional Idempotency Boundary**:
1. **Deduplication Key:** Every AMQP message generated from the Outbox carries a unique identifier (`ea.BasicProperties.MessageId`).
2. **Relational Tracking Entity:** We introduce a dedicated database entity and table:
   ```csharp
   public sealed class MaintenanceCompletionRecord
   {
       public Guid MessageId { get; private set; }
       public Guid MaintenanceId { get; private set; }
       public Guid VehicleId { get; private set; }
       public DateTimeOffset CompletedOnUtc { get; private set; }
       public DateTimeOffset ProcessedOnUtc { get; private set; }
   }
   ```
   where `MessageId` is the primary key.
3. **Atomic Relational Execution:**
   The consumer use case (`ProcessMaintenanceCompletedUseCase`) runs the deduplication check and the business effect within the **exact same relational transaction**:
   ```csharp
   if (await _recordRepository.ExistsAsync(command.MessageId, cancellationToken))
   {
       return; // Already processed; skip gracefully
   }
   await _recordRepository.AddAsync(...);
   await _unitOfWork.SaveChangesAsync(cancellationToken);
   ```
4. **Primary Key Collision Defense:**
   If two concurrent consumer threads receive the same duplicate message simultaneously, both pass the `ExistsAsync` check, but the physical PostgreSQL primary key constraint (`23505 unique_violation`) rejects the second insert, rolling back its transaction cleanly.
5. **Strict Post-Commit ACK:**
   `channel.BasicAckAsync` is executed **only after** the database transaction has successfully committed. If the process crashes before the ACK, RabbitMQ redelivers the message with `Redelivered == true`; upon redelivery, `ExistsAsync` returns true, the duplicate business effect is bypassed, and the message is safely acknowledged.

## Alternatives Considered
1. **In-Memory Cache Deduplication (ConcurrentDictionary / MemoryCache):**
   - *Rejection:* Lost upon process crash or pod restart; completely ineffective in multi-replica deployments.
2. **Redis SETNX / Distributed Idempotency Key:**
   - *Rejection:* Disconnects the idempotency record from the relational database commit (dual-write hazard). If Redis records the key, but the database transaction subsequently fails, the message is permanently dropped and the business effect is never recorded.
3. **Natural Idempotency (Blind Overwrites):**
   - *Rejection:* Only works for simple state assignments (e.g., `status = 'Completed'`); fails for additive or append-only ledger records.

## Why This Decision
- Guarantees that the business record and the deduplication record commit or rollback together atomically.
- Zero risk of phantom deduplication (recording processed when database failed).
- Eliminates duplicate business effects even under adversarial crash scenarios.

## Trade-offs
- Requires an additional database write per consumed message to store the completion record.
- Table growth requires periodic archival of old processed message identifiers after the retention/retry window has elapsed.

## Consequences
- The audited consumer pipeline guarantees a **Single Business Effect** regardless of network retransmissions or broker duplicate redeliveries.
- The consumer has no external non-transactional side-effects (e.g., no raw external HTTP calls or uncoordinated file writes), ensuring that database rollback completely restores clean state.

## Risks
- If a developer adds an external non-transactional side-effect (e.g., sending an external email or charging a payment gateway) inside the use case before the database commit, that side effect would not be rolled back. 
- *Mitigation:* Architecture guidelines dictate that third-party non-transactional integrations must follow their own outbox or two-phase state machine.

## Operational Impact
- Primary key index on `maintenance_completion_records.message_id` provides sub-millisecond deduplication checks.

## Future Evolution
- Create a generalized generic inbox table (`inbox_messages`) for use cases that require multi-aggregate consumption workflows.
