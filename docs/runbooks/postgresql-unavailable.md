# Runbook: PostgreSQL Database Unavailable

## 1. Overview & Severity
* **Service:** FleetOps API / Relational Storage
* **Severity:** CRITICAL (Outage)
* **Architectural Impact:** PostgreSQL is the single source of truth for all domain entities and the Transactional Outbox. If PostgreSQL is unreachable, the API cannot process read or write requests.

---

## 2. Symptoms & Detection
* **Health Probes:**
  * `GET /health/live`: HTTP 200 OK (Kestrel process alive).
  * `GET /health/ready`: **HTTP 503 Service Unavailable** (Traffic immediately dropped from load balancer).
* **Metrics:**
  * `fleetops.http.requests.total` shows surge in `status_code: 500`.
* **User Impact:** API returns RFC 9457 `ProblemDetails` with HTTP 500 without leaking connection credentials.

---

## 3. Immediate Triage & Diagnosis
1. Check PostgreSQL container / service status:
   ```bash
   docker compose ps postgres
   docker compose logs --tail=100 postgres
   ```
2. Test physical database connection with `pg_isready`:
   ```bash
   docker compose exec postgres pg_isready -h localhost -p 5432 -U postgres
   ```
3. Check PostgreSQL connection pool exhaustion:
   ```sql
   SELECT count(*) FROM pg_stat_activity WHERE datname = 'fleetops';
   ```

---

## 4. Containment & Mitigation
1. If PostgreSQL container stopped unexpectedly:
   ```bash
   docker compose up -d postgres
   ```
2. If disk space is full on PostgreSQL data volume:
   ```bash
   df -h /var/lib/postgresql/data
   ```
3. If connections are exhausted, terminate idle transactions:
   ```sql
   SELECT pg_terminate_backend(pid) 
   FROM pg_stat_activity 
   WHERE state = 'idle in transaction' AND state_change < now() - INTERVAL '5 minutes';
   ```

---

## 5. Recovery & Verification
1. Verify readiness probe recovers:
   ```bash
   curl -i http://localhost:5000/health/ready
   ```
   Must return **HTTP 200 OK**.
2. Verify traffic flows through load balancer.
3. Verify that zero partial or corrupted state occurred during downtime.
