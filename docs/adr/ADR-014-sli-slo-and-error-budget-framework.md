# ADR-014 — Service Level Indicator (SLI), Service Level Objective (SLO), and Error Budget Framework

## Status
Accepted

## Context
Operational reliability requires quantitative targets that define acceptable performance and availability. Without explicit Service Level Objectives (SLOs), engineering teams either over-engineer for unachievable perfection (100% uptime) or neglect critical latency regressions.

## Problem
Organizations frequently conflate contractual SLAs with internal operational SLOs. Furthermore, arbitrary targets (e.g., "four nines" 99.99% or "sub-10ms globally") chosen without baseline measurements create false alarms and alert fatigue.

## Decision
We establish an internal **SLI/SLO Framework** based on empirical performance measurements:
1. **Core SLIs (Service Level Indicators):**
   - **SLI-1 (Availability):** Ratio of successful HTTP requests (HTTP status $< 500$) divided by total valid requests.
     $$\text{SLI}_{\text{Availability}} = \frac{\text{Count}(\text{status} < 500)}{\text{Count}(\text{total requests})}$$
   - **SLI-2 (Read/Probe Latency):** Percentage of health and query requests completing in under 150ms.
     $$\text{SLI}_{\text{Latency}} = \frac{\text{Count}(\text{duration} \le 150\text{ms})}{\text{Count}(\text{total requests})}$$
   - **SLI-3 (Outbox Processing Lag):** Time elapsed between message insertion in `outbox_messages` (`occurred_on_utc`) and successful broker publish confirmation (`processed_on_utc`).
   - **SLI-4 (Consumer Deduplication & Reliability):** Ratio of successfully processed or deduplicated events divided by total deliveries.
2. **SLO Targets (30-day Rolling Window):**
   - **Availability SLO:** $99.9\%$ (three nines) for core API operations.
   - **Latency SLO:** $95\%$ of read/probe requests complete within $150\text{ms}$ ($P_{95} \le 150\text{ms}$), $99\%$ complete within $300\text{ms}$ ($P_{99} \le 300\text{ms}$).
   - **Outbox Processing SLO:** $99\%$ of outbox events are published within 5 seconds under normal broker operation.
   - **Consumer Success SLO:** $99.9\%$ of valid domain events are processed without unhandled termination.
3. **Error Budget Policy:**
   - For an availability SLO of $99.9\%$, the allowable Error Budget is $0.1\%$.
   - Over a 30-day window with 1,000,000 requests, the error budget permits 1,000 failed requests.
   - **Error Budget Burn Rate Alerts:** If the burn rate exceeds $2\%$ of the error budget within 1 hour, P1 alerts notify on-call engineers. If the error budget is exhausted ($100\%$ consumed), non-critical feature releases are paused to focus on reliability engineering.

## Alternatives Considered
1. **Targeting 99.99% Availability Without Redundant Multi-Region Infrastructure:**
   - *Rejection:* Technologically dishonest for a single-region deployment with planned maintenance windows.

## Why This Decision
- Balances velocity with operational reliability.
- Provides objective criteria for on-call alerting and feature freezes.

## Consequences
- Operations dashboards track SLIs directly via Prometheus / OpenTelemetry metrics.
