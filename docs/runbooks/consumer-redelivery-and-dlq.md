# Runbook: Consumer Redelivery Spikes & Dead-Letter Queue (DLQ) Remediation

## 1. Overview & Severity
* **Service:** FleetOps Messaging Consumer (`MaintenanceCompletedConsumer`)
* **Severity:** Medium to High
* **Description:** Messages arriving in the Dead-Letter Queue (`fleetops.vehicle-maintenance.completed.dlq`) or experiencing excessive redelivery cycles.

---

## 2. Symptoms & Detection
* **Alert:** Messages detected in DLQ ($> 0$).
* **Metrics:**
  * `fleetops.consumer.messages.failed.total` increments.
  * RabbitMQ Management UI shows `Messages > 0` in queue `fleetops.vehicle-maintenance.completed.dlq`.
* **Logs:**
  ```text
  [ERROR] Exhausted maximum retry attempts (3) for message {MessageId}. Routing to DLQ.
  ```

---

## 3. Immediate Triage & Diagnosis
1. Inspect DLQ messages via RabbitMQ Management API or CLI:
   ```bash
   rabbitmqadmin get queue=fleetops.vehicle-maintenance.completed.dlq count=5
   ```
2. Inspect headers of the dead-lettered message:
   - `x-dlq-reason`: Why the message was routed to DLQ (`MaxRetriesExhausted`, `PoisonMessage`, etc.).
   - `x-retry-count`: Total retries attempted (typically 3).
   - `x-exception-message`: Specific exception that caused the transient failures.
3. Check database consistency:
   Check if the message was partially committed:
   ```sql
   SELECT * FROM maintenance_completion_records WHERE message_id = '{MessageId}';
   ```

---

## 4. Containment & Mitigation
1. If failure is due to a bug in consumer code (e.g. unhandled null reference or domain rule conflict):
   - Deploy bugfix release.
   - Do NOT delete messages from the DLQ.
2. If failure is due to an unrecoverable poison payload (corrupted JSON or missing mandatory IDs):
   - Export message payload for post-mortem audit.
   - Acknowledge / purge only after recording audit evidence.

---

## 5. Recovery & DLQ Replay
1. Once the root cause is resolved, replay messages from DLQ to the main exchange:
   ```bash
   # Re-publish message to main topic exchange with routing key
   rabbitmqadmin publish exchange=fleetops.events routing_key=maintenance.completed payload='{...}'
   ```
2. Observe consumer processing and idempotency deduplication:
   `fleetops.consumer.messages.processed.total` increments.
3. Verify that the DLQ is clean and depth returns to 0.
