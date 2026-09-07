# ADR-010 — In-Process Application Rate Limiting with Boundary Demarcation

## Status
Accepted

## Context
FleetOps API serves operational REST endpoints for fleet assets. High concurrency, burst traffic, or misconfigured clients can trigger thread pool starvation, connection pool depletion, and database CPU spikes.

In ADR-009, we established the boundary demarcation between local process protection and edge/gateway rate limiting. In this ADR, we formalize the implementation of local in-process rate limiting.

## Problem
Relying solely on external gateways leaves individual pods vulnerable to intra-cluster noisy neighbors or direct internal service-to-service traffic. Conversely, relying on an in-memory application rate limiter and marketing it as "global distributed DDoS protection" is an architectural anti-pattern, because in a horizontally scaled cluster each pod tracks only its local window.

## Decision
We implement **In-Process Fixed Window Rate Limiting** using ASP.NET Core 10's native `Microsoft.AspNetCore.RateLimiting` middleware:
1. **Local Capacity Protection:** Configured with a default policy (`FleetOpsRateLimit`) enforcing a fixed window (default: 100 requests per 60 seconds per process) with zero queue limit (`QueueLimit = 0`) to immediately reject excess load and avoid memory bloat.
2. **Standardized Rejection Contract:** Rejected requests immediately return **HTTP 429 Too Many Requests** with standard `application/problem+json` content conforming to **RFC 9457 / RFC 6585** and include a `Retry-After: 1` header.
3. **Probe Exemption:** Diagnostic probes (`/health/live`, `/health/ready`, `/health/dependencies`) and OpenAPI endpoints bypass rate limiting to prevent health checking systems from triggering false-positive pod restarts.
4. **Boundary Demarcation:** The system explicitly documents that multi-replica distributed quota enforcement belongs to the network perimeter (Ingress / API Gateway / Cloudflare / AWS WAF), preserving application autonomy without external Redis dependencies.

## Alternatives Considered
1. **Application-Level Redis Rate Limiter:**
   - *Rejection:* Violates ADR-004 by introducing Redis into the primary HTTP request path, adding network latency and a stateful failure point.
2. **Unbounded Queueing in Memory:**
   - *Rejection:* Queueing requests during overload exhausts server RAM and causes cascading timeouts.

## Why This Decision
- Zero external infrastructure dependencies.
- Sub-microsecond latency overhead.
- Protects Kestrel threads and database connection pools from local spikes.

## Trade-offs
- Limits are enforced per replica instance, not globally across the entire cluster.

## Consequences
- Endpoints return structured RFC 9457 `ProblemDetails` upon reaching capacity limit.
- Infrastructure operators must configure edge rate limiting at the Ingress controller for global enforcement.

## Operational Impact
- Monitor HTTP 429 response counts via `fleetops.http.requests.total` with tag `status_code: 429`.
