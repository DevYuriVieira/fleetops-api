# Runbook: RabbitMQ Broker Unavailable

## 1. Overview & Severity
* **Service:** FleetOps API / Messaging Subsystem
* **Severity:** Medium (Degraded)
* **Architectural Impact:** Under the **Transactional Outbox Pattern**, command ingestion continues uninterrupted. Write commands commit atomically to PostgreSQL. The Outbox background worker enters a backoff retry state and pending events accumulate in `outbox_messages`.

---

## 2. Symptoms & Detection
* **Health Probe:** `GET /health/dependencies` returns HTTP 200 with status `"Degraded"` and entry `"rabbitmq": "Degraded"`. Note: `/health/ready` remains **HTTP 200 OK**.
* **Metrics:**
  * `fleetops.outbox.messages.failed.total` increments.
  * `fleetops.outbox.messages.pending` increases monotonically.
* **Logs:**
  ```text
  [WARN] Failed to connect to RabbitMQ broker at 127.0.0.1:5672. Retrying in 2000ms...
  ```

---

## 3. Immediate Triage & Diagnosis
1. Verify RabbitMQ container / cluster state:
   ```bash
   docker compose ps rabbitmq
   docker compose logs --tail=100 rabbitmq
   ```
2. Test AMQP port reachability from API container:
   ```bash
   nc -zv rabbitmq 5672
   ```
3. Inspect pending messages in PostgreSQL:
   ```sql
   SELECT COUNT(*), MIN(occurred_on_utc) AS oldest_pending
   FROM outbox_messages
   WHERE processed_on_utc IS NULL;
   ```

---

## 4. Containment & Mitigation
* **Do NOT restart API pods.** Restarting API pods does not resolve broker reachability and terminates in-flight HTTP requests.
* If container is stopped, restart the broker:
   ```bash
   docker compose restart rabbitmq
   ```
* If disk space / memory alarm is triggered on RabbitMQ:
   ```bash
   rabbitmq-diagnostics status
   rabbitmqctl set_disk_free_limit 1GB
   ```

---

## 5. Recovery & Verification
1. Once RabbitMQ is healthy, the Outbox worker automatically reconnects via `RabbitMqConnection.GetConnectionAsync`.
2. Observe backlog draining:
   ```sql
   SELECT COUNT(*) FROM outbox_messages WHERE processed_on_utc IS NULL;
   ```
3. Check `/health/dependencies`:
   ```bash
   curl -s http://localhost:5000/health/dependencies | jq .
   ```
   Must return `"status": "Healthy"`.
4. Verify broker confirm metric resumes incrementing:
   `fleetops.outbox.messages.published.total`.
