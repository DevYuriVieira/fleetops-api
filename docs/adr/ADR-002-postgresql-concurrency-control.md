# ADR-002 — PostgreSQL Concurrency Control via FOR UPDATE SKIP LOCKED and xmin Optimistic Tokens

## Status
Accepted

## Context
FleetOps API operates in concurrent, multi-replica environments. Two primary concurrency challenges arise:
1. **Outbox Worker Coordination:** When multiple application pods run parallel background workers polling the `outbox_messages` table, they must claim disjoint batches of messages without processing the same message concurrently and without blocking each other.
2. **Aggregate State Mutations:** Multiple API requests may concurrently attempt to modify the same business entity (e.g., updating vehicle mileage or reassigning drivers).

## Problem
Traditional concurrency control mechanisms exhibit severe limitations:
- Standard `SELECT ... FOR UPDATE` causes concurrent workers to block and wait for locks held by other transactions, creating thread starvation, latency spikes, and potential deadlocks.
- Distributed locks (e.g., Redis Redlock) introduce network roundtrips, extra infrastructure dependencies, split-brain risks during network partitions, and clock drift vulnerabilities.
- Plain application-level locking (e.g., in-memory semaphores or mutexes) only protects a single operating system process and fails completely in multi-replica deployments.

## Decision
We implement a two-tier PostgreSQL-native concurrency strategy:

1. **Outbox Polling with `FOR UPDATE SKIP LOCKED`:**
   In `OutboxService.cs`, message batches are fetched using raw SQL within an explicit database transaction:
   ```sql
   SELECT id, occurred_on_utc, event_type, payload, processed_on_utc, attempts, error, trace_parent
   FROM outbox_messages
   WHERE processed_on_utc IS NULL AND attempts < {0}
   ORDER BY occurred_on_utc
   LIMIT {1}
   FOR UPDATE SKIP LOCKED
   ```
   PostgreSQL skips any rows currently locked by concurrent transactions and returns only unlocked rows, enabling lock-free horizontal scaling of workers against a shared database.

2. **Domain Optimistic Concurrency via PostgreSQL `xmin`:**
   In entity type configurations (e.g., `VehicleConfiguration.cs`), aggregate roots map the PostgreSQL system column `xmin` as their concurrency token:
   ```csharp
   builder.Property<uint>("Version").IsRowVersion();
   ```
   Under PostgreSQL, Npgsql maps `IsRowVersion()` to the `xmin` system column (type `xid`), which records the transaction ID that last inserted or updated the row. If concurrent transactions attempt to update the same aggregate, EF Core detects the version mismatch, throws `DbUpdateConcurrencyException`, and the API responds with HTTP 409 Conflict without corrupting state.

3. **Domain Mutual Exclusion via Partial Unique Indexes:**
   For exclusive domain states (such as ensuring a vehicle has at most one active maintenance order), we enforce physical database constraints:
   ```sql
   CREATE UNIQUE INDEX ix_maintenances_vehicle_id 
   ON maintenances (vehicle_id) 
   WHERE status IN ('Scheduled', 'InProgress');
   ```

## Alternatives Considered
1. **External Distributed Lock (Redis / Redlock / Consul):**
   - *Rejection:* Adds infrastructure complexity, failure modes during network partitions, and dual-write concerns.
2. **PostgreSQL Advisory Locks (`pg_advisory_lock`):**
   - *Rejection:* Coarse-grained (session-level or transaction-level lock identifiers) and prone to leak if connection pooling is not meticulously managed.
3. **Application-Level Mutex:**
   - *Rejection:* Ineffective across multiple pods / containers.

## Why This Decision
- Uses native relational engine primitives with zero external operational dependencies.
- `FOR UPDATE SKIP LOCKED` provides deterministic queue-like semantics directly inside PostgreSQL with zero lock contention.
- `xmin` provides automatic row-level optimistic concurrency without requiring application code to manually increment and track integer version columns.

## Trade-offs
- **PostgreSQL Specificity:** `FOR UPDATE SKIP LOCKED` and `xmin` tie the database layer to PostgreSQL dialect semantics. This is an accepted trade-off since PostgreSQL is our designated database engine.
- **Outbox Message Ordering:** While messages are ordered by `occurred_on_utc`, parallel workers processing disjoint batches may complete in slightly different order across distinct aggregates. Aggregate-level strict sequential processing is not guaranteed across distinct workers.

## Consequences
- Workers can scale horizontally without deadlock risk under the verified workload.
- Concurrent updates to the same entity fail safely with HTTP 409 Conflict.
- No third-party distributed locking software is required.

## Risks
- Extreme transaction commit rates on the outbox table require routine autovacuum monitoring to prevent table bloat.

## Operational Impact
- Requires autovacuum tuning on PostgreSQL for high-write tables (`outbox_messages`).

## Future Evolution
- If partitioning by aggregate ID is required for strict FIFO processing per vehicle, PostgreSQL advisory locks hashed by `vehicle_id` can be combined with `SKIP LOCKED`.
