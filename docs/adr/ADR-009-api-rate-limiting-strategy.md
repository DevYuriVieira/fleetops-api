# ADR-009 — Boundary Demarcation Between In-Memory Application Rate Limiting and Global Gateway Enforcement

## Status
Accepted

## Context
APIs exposed to network traffic require rate limiting to prevent Denial of Service (DoS), resource exhaustion, brute-force credential stuffing, and noisy neighbor interference.

We evaluated the architectural placement of rate limiting for FleetOps API.

## Problem
In modern containerized deployments, backend APIs typically run as multiple horizontal replicas behind a load balancer or Ingress controller.

A common architectural anti-pattern is configuring an in-memory application rate limiter (e.g., .NET's `System.Threading.RateLimiting`) and claiming that the system possesses "enterprise distributed rate limiting":
- In-memory rate limiters maintain counters in process memory.
- If an API runs with 5 replicas and each pod configures a limit of 100 requests per minute per IP, a client can issue up to **500 requests per minute** by distributing requests across the replicas.
- In-memory rate limiting provides local process self-defense against thread starvation, but does **not** provide global or cluster-wide traffic quota enforcement.

Conversely, implementing global distributed rate limiting directly inside the application pod typically requires introducing a shared Redis cluster, bringing back the operational dependencies, latency overhead, and failure modes described in ADR-004.

## Decision
We establish an explicit **Boundary Demarcation Strategy**:
1. **Application Layer Boundary:**
   The application does not maintain a custom distributed rate limiting cache. If in-process rate limiting is configured via ASP.NET Core middleware (`AddRateLimiter`), it is documented strictly as **Local Process Protection**, guarding individual pods from sudden burst CPU/thread starvation.
2. **Infrastructure / Gateway Boundary:**
   Cluster-wide, global, and client-tier rate limiting is formally delegated to the **Edge / Ingress / API Gateway layer** (e.g., Cloudflare, AWS WAF, Kong, Envoy, or NGINX Ingress Controller). The gateway layer operates at the network perimeter, terminates client connections, and enforces distributed IP/Token sliding-window rate limits before traffic reaches the internal service mesh or container pods.
3. **Absence of Overclaiming:**
   We strictly refuse to market in-memory process rate limiters as "enterprise distributed rate limiting".

## Alternatives Considered
1. **In-Application Redis-Backed Distributed Rate Limiter:**
   - *Rejection:* Forces the API to depend on a distributed Redis cluster solely for counter increments, increasing request latency and creating an external point of failure on the critical HTTP path.
2. **Relying Exclusively on In-Memory Rate Limiting Without Qualification:**
   - *Rejection:* Dishonest architectural communication. It gives false confidence that a cluster is protected against abuse when it actually scales the allowable request quota linearly with replica count.

## Why This Decision
- Places traffic shaping and DoS mitigation at the network edge where it belongs, saving backend compute, memory, and database connection pool capacity.
- Preserves backend simplicity and autonomy.
- Adheres to the principle of clear boundary demarcation: infrastructure concerns at the gateway; domain and transactional consistency at the backend.

## Trade-offs
- Local standalone deployments without an API Gateway in front have no cluster-wide rate coordination (each instance enforces its own local limit).

## Consequences
- Production deployments must configure rate limits (e.g., 100 req/min per IP) in their Ingress / Gateway specifications.
- Error responses for exceeded limits return RFC 9457 `ProblemDetails` with HTTP 429 Too Many Requests.

## Risks
- If operations exposes the backend directly to the public internet without an Ingress controller or Gateway, cluster-wide rate limiting will be absent.
- *Mitigation:* Deployment topology documentation explicitly mandates placing an Ingress / Reverse Proxy in front of production containers.

## Operational Impact
- Rate limit metrics (429 counts) are monitored at the Ingress/Gateway dashboard.

## Future Evolution
- If fine-grained user-tier rate limiting (e.g., dynamic limits based on customer subscription plans) is required, evaluate Kong or Envoy plugin integration rather than embedding distributed state inside the API runtime.
