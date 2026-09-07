# ADR-013 — Empirical Load Testing and Performance Baseline Methodology via k6

## Status
Accepted

## Context
Performance claims in backend distributed systems are frequently compromised by unsubstantiated estimates ("supports 10,000 RPS", "zero latency"). Production engineering requires reproducible benchmarks executed against real infrastructure under controlled scenarios.

## Problem
Without automated and versioned performance scripts:
- Performance regressions go unnoticed until production deployment.
- Developers cannot reproduce baseline latency percentiles ($P_{50}, P_{95}, P_{99}$).
- Ad-hoc manual testing produces wildly inconsistent numbers due to varying machine load and untracked parameters.

## Decision
We adopt **k6** as the standardized load testing tool for FleetOps API:
1. **Versioned Scenarios in Repository:**
   All test scenarios are committed under `load-tests/`:
   - `k6-baseline.js`: Low concurrency (5 VUs, 30s) testing health probes and read endpoints to measure baseline steady-state latency.
   - `k6-sustained.js`: Sustained concurrency (15–20 VUs, 30s) measuring system throughput under continuous traffic.
   - `k6-spike.js`: Traffic spike (ramp to 40 VUs) observing rate limiting behavior (HTTP 429) and post-spike recovery.
2. **Empirical Measurement Rule:**
   All performance metrics documented in the repository must be derived from **actual measured executions** recorded in `load-tests/` or automated benchmarks.
3. **Pipeline Placement:**
   Heavy stress benchmarks are not part of the standard commit PR validation to prevent CI pipeline flakiness and high cloud runner costs. They are designated as on-demand and pre-release performance validation gates.

## Alternatives Considered
1. **Bombardier:**
   - *Consideration:* Extremely fast Go-based HTTP benchmark tool.
   - *Rejection:* Lacks multi-step scripting, setup hooks (e.g. acquiring JWT tokens), and custom assertion thresholds.
2. **JMeter:**
   - *Rejection:* Heavy XML configurations, high resource footprint, poor developer ergonomics.

## Why This Decision
- k6 uses lightweight JavaScript for test definitions.
- Provides native percentile calculation ($P_{50}, P_{95}, P_{99}$) and threshold assertions.
- Seamlessly outputs metrics to console, JSON, or OpenTelemetry collectors.

## Consequences
- Any engineer can execute `load-tests/run-benchmarks.ps1` and reproduce the system's baseline metrics.
