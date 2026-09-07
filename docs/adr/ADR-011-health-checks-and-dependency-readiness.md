# ADR-011 — Granular Health Semantics under the Transactional Outbox Pattern

## Status
Accepted

## Context
Orchestrators (Kubernetes, Docker Swarm, Nomad) rely on HTTP health probes to decide whether to restart containers (liveness) or route incoming network traffic to them (readiness).

FleetOps uses PostgreSQL 18 for domain state and the Transactional Outbox pattern, with RabbitMQ 3.13 as the asynchronous message broker.

## Problem
A naive approach is to group all dependencies into a single `/health` endpoint that returns HTTP 503 if ANY dependency fails. Under this flawed design:
- If RabbitMQ suffers a momentary outage, the entire API returns 503 and is marked unready by the load balancer.
- All write requests (e.g., registering vehicles, assigning drivers, scheduling maintenance) are rejected, even though PostgreSQL is fully operational and capable of storing business transactions and Outbox messages atomically!
- This completely negates the primary architectural benefit of the **Transactional Outbox Pattern** (decoupling command ingestion availability from broker availability).

## Decision
We implement a three-tier granular health probing strategy:
1. **Liveness Probe (`/health/live`):**
   - Answers: *Is the process running and responding to HTTP?*
   - Checks: Evaluates zero external dependencies. Always returns **HTTP 200 OK** if Kestrel is active.
   - Purpose: Directs orchestrators NOT to restart the container if dependencies are degraded.
2. **Readiness Probe (`/health/ready`):**
   - Answers: *Can the API accept and persist business write transactions right now?*
   - Checks: Verifies PostgreSQL connectivity (`DatabaseHealthCheck`).
   - Returns: **HTTP 200 OK** if PostgreSQL is reachable; **HTTP 503 Service Unavailable** if PostgreSQL is down.
   - Purpose: Directs load balancers to route write and read traffic to this pod.
3. **Dependency Diagnostics Probe (`/health/dependencies`):**
   - Answers: *What is the exact health state of each backing dependency?*
   - Checks: PostgreSQL and RabbitMQ (`RabbitMqHealthCheck`).
   - Degraded State Semantic: If PostgreSQL is UP but RabbitMQ is DOWN, the endpoint returns **HTTP 200 OK with `status: "Degraded"`**.
   - Detail: Explicitly indicates that the API is functioning in **Transactional Outbox Accumulation Mode**, accepting writes while the background worker queues events for later broker recovery.

## Alternatives Considered
1. **Coupling RabbitMQ into `/health/ready`:**
   - *Rejection:* Disastrous availability failure. Causes healthy API pods to be removed from load balancing whenever RabbitMQ restarts or undergoes maintenance.
2. **Single Monolithic `/health` Check:**
   - *Rejection:* Conflates process health with database readiness and broker health.

## Why This Decision
- Preserves high write availability during broker maintenance.
- Maximizes the architectural value of the Transactional Outbox.
- Conforms to standard cloud-native Kubernetes probe semantics.

## Trade-offs
- Monitoring systems must inspect the JSON body of `/health/dependencies` or metric counters rather than relying solely on HTTP status codes to detect broker degradation.

## Consequences
- Operators can perform zero-downtime maintenance on RabbitMQ without impacting API command ingestion.
- Pending outbox messages accumulate in `outbox_messages` and drain automatically upon broker recovery.
