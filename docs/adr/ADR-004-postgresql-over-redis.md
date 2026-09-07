# ADR-004 — Relational ACID and Index-Based Concurrency over Distributed In-Memory Caching (Redis)

## Status
Accepted

## Context
In distributed .NET architectures, Redis is frequently introduced for distributed locking (Redlock), distributed caching (`IDistributedCache`), message deduplication, and rate limiting.

We evaluated whether Redis should be introduced into the FleetOps API infrastructure stack.

## Problem
Introducing Redis into a transactional operations backend brings distinct operational and architectural liabilities:
1. **Cache Invalidation Complexity:** In fleet management, operational data (vehicle mileage, driver assignments, maintenance status) changes rapidly via transactional commands. In-memory caching introduces the risk of stale reads, cache stampedes, and cache invalidation race conditions.
2. **Dual-Write Hazards on Write-Through Cache:** If application code writes to PostgreSQL and subsequently updates Redis, an unexpected failure between the two writes introduces data desynchronization and split-brain states.
3. **Operational Burden & Attack Surface:** Adding Redis adds an additional stateful container to maintain, patch, monitor, secure with TLS, and configure with persistence (RDB/AOF) and replication (Sentinel/Cluster).

## Decision
We intentionally **exclude Redis** from the FleetOps architecture for the current scope.
Instead, we rely entirely on PostgreSQL's native capabilities:
1. **Concurrency Control:** Managed by PostgreSQL `FOR UPDATE SKIP LOCKED` for worker polling and `xmin` row versioning for optimistic locking.
2. **Mutual Exclusion & State Integrity:** Managed by PostgreSQL partial unique indexes (`CREATE UNIQUE INDEX ... WHERE status IN (...)`).
3. **Consumer Idempotency:** Managed by a dedicated relational table (`maintenance_completion_records`) with primary key `message_id` within the atomic business transaction.
4. **Task Polling:** Managed by the Transactional Outbox table in PostgreSQL.

## Alternatives Considered
1. **Redis for Outbox Worker Coordination (Redlock):**
   - *Rejection:* Redlock relies on synchronizing system wall clocks across multiple nodes, has well-documented failure modes during GC pauses and network partitions, and is completely unnecessary given PostgreSQL's native `SKIP LOCKED`.
2. **Redis for Consumer Idempotency Cache (SETNX / Exists):**
   - *Rejection:* If Redis records a message as processed, but the database commit fails, the business state is lost forever. Storing idempotency inside the relational database guarantees that the idempotency record and the business effect commit or rollback atomically.
3. **Redis as Secondary Read Cache:**
   - *Rejection:* The current workload is write-heavy and transactional. Read queries benefit directly from PostgreSQL's memory-backed buffer pool (`shared_buffers`) without cache invalidation complexity.

## Why This Decision
- Rejection of **Resume-Driven Development (RDD)**: We do not add infrastructure components unless they solve an active bottleneck that existing components cannot address.
- Guarantees strict single-source-of-truth transactional consistency.
- Keeps deployment topology simple: only PostgreSQL, RabbitMQ, and the API container.

## Trade-offs
- If the application later requires extreme read-heavy throughput (e.g., millions of public vehicle geolocation lookups per second), PostgreSQL alone would reach scalability limits without read replicas or distributed caching.
- Distributed rate limiting across multiple pods must be handled by an API Gateway or Ingress rather than in-process shared Redis state.

## Consequences
- Zero dual-write risk between relational storage and in-memory cache.
- Simpler local development, CI pipelines, and deployment topology.

## Risks
- Elevated database CPU/IO if read patterns shift dramatically. Mitigated by proper relational indexing, connection pooling, and read replicas if needed in the future.

## Operational Impact
- Reduced infrastructure footprint: one less stateful cluster to manage in production.

## Future Evolution
- If high-frequency read-heavy endpoints are added (e.g., telemetry dashboard queries), Redis or an in-memory CDN layer may be evaluated strictly as an ephemeral, read-only cache behind an explicit TTL policy.
