# ADR-015 — Capacity Engineering, Domain-Level Workloads, and Performance Boundary Validation

## Status
Accepted

## Context
In Sprint P1, FleetOps API established an empirical performance baseline utilizing k6 against local infrastructure, observing ~808 RPS with sub-millisecond $P_{50}$ latency. However, rigorous adversarial inspection revealed that this workload targeted exclusively in-memory diagnostic health probes (`/health/live`), where Kestrel responds without database round-trips, change tracking, or broker publishing.

In production engineering, conflating health probe throughput with business domain transaction capacity is a critical anti-pattern. Real domain writes (`POST /api/vehicles`) execute cryptographic JWT signature validation, domain aggregate construction, relational ACID transactions with MVCC `xmin` row-versioning in PostgreSQL, Transactional Outbox serialization and persistence, background worker polling with `FOR UPDATE SKIP LOCKED`, and AMQP message publishing with Publisher Confirms.

## Problem
1. **False Equivalency:** Extrapolating in-memory probe speeds to transactional business capacity creates unrealistic expectations and masks database bottlenecks.
2. **Artificial Rate Limiting Caps:** The in-process rate limiter (default: 100 requests per 60 seconds) appropriately protects local node resources, but halts capacity tests after 100 requests, preventing empirical discovery of the true database saturation knee.
3. **Unverified Failure Under Load:** Verifying graceful degradation under single-threaded conditions is insufficient; the system must demonstrate how Outbox backlogs accumulate and recover when broker outages coincide with concurrent client writes.

## Decision
We institutionalize **Capacity Engineering and Empirical Domain-Level Performance Validation** for FleetOps API:

1. **Strict Workload Categorization:**
   - **Infrastructure / Health Baseline:** Dedicated to validating Kestrel pipeline efficiency and diagnostic responsiveness (`/health/live`, `/health/dependencies`). Retained as an operational benchmark for HTTP server health.
   - **Domain Capacity Workloads:** Dedicated to measuring authenticated business write transactions (`POST /api/vehicles`) under real PostgreSQL and RabbitMQ operations.
2. **Standardized Domain Scenarios in `load-tests/`:**
   - `k6-domain-baseline.js`: Low, steady-state concurrency (5 VUs, 30s) measuring nominal write latency percentiles ($P_{50}, P_{90}, P_{95}$).
   - `k6-domain-sustained.js`: Sustained concurrency (15 VUs, 45s) evaluating continuous write throughput, connection pool durability, and outbox creation rate.
   - `k6-domain-spike.js`: Abrupt burst traffic (5 &rarr; 35 &rarr; 5 VUs) observing latency inflection, connection queueing, and rapid recovery.
   - `k6-domain-stress.js`: Multi-stage progressive ramp (5 &rarr; 15 &rarr; 30 &rarr; 50 &rarr; 70 VUs) designed to push the system to its empirical saturation point.
   - `k6-ratelimit-burst.js`: Dedicated rate-limiting validation verifying immediate HTTP 429 rejection, RFC 9457 `ProblemDetails` compliance, and `Retry-After` header delivery under default capacity limits.
3. **Explicit Capacity Profile:**
   - For saturation and capacity testing, an explicit benchmark profile (`RateLimiting:PermitLimit = 100000`) is utilized to allow load to reach the underlying database and messaging layers unthrottled. This profile is never deployed silently to standard production.
4. **Empirical Saturation Definition:**
   - Saturation is defined not by arbitrary numbers, but by observable indicators: abrupt latency escalation ($P_{95} > 500\text{ms}$), database connection pool contention, consumer lag accumulation, or throughput flattening despite increasing concurrency.
5. **Decoupled Performance CI Workflow:**
   - Heavy performance benchmarks are decoupled from per-commit PR validation and isolated in `.github/workflows/performance.yml` (triggered via `workflow_dispatch` or weekly schedule), preserving fast, deterministic CI pipelines.

## Alternatives Considered
1. **Mocking Database / Broker During Domain Benchmarks:**
   - *Rejection:* Mocking external dependencies produces artificial throughput figures that fail to reflect real PostgreSQL disk flush (WAL) and RabbitMQ socket latency.
2. **Running Continuous Stress Tests in Every PR:**
   - *Rejection:* Causes CI flakiness, exhausts GitHub Actions runner limits, and incurs unnecessary infrastructure costs.

## Consequences
- The system establishes an honest, verifiable boundary between in-memory HTTP responsiveness and relational ACID transaction throughput.
- SRE teams obtain reproducible baseline figures for capacity planning, horizontal autoscaling thresholds, and database connection pool tuning.
- All documented performance figures represent actual measured executions with explicit environment disclosure.
