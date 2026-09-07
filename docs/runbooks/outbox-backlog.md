# Runbook: Outbox Backlog & Worker Lag

## 1. Overview & Severity
* **Service:** FleetOps API / OutboxProcessor
* **Severity:** Medium
* **Description:** Outbox events are accumulating in the `outbox_messages` table faster than the background worker can publish and confirm them with RabbitMQ.

---

## 2. Symptoms & Detection
* **Alert:** Outbox oldest pending message age $> 30\text{ seconds}$ or pending count $> 5,000$.
* **Metrics:**
  * `fleetops.outbox.messages.pending` rising continuously.
  * `fleetops.outbox.publish.duration.ms` $P_{95} > 500\text{ms}$.
* **PostgreSQL Query:**
  ```sql
  SELECT COUNT(*), MIN(occurred_on_utc) AS oldest_pending, MAX(attempts) AS max_attempts
  FROM outbox_messages
  WHERE processed_on_utc IS NULL;
  ```

---

## 3. Immediate Triage & Diagnosis
1. Check if worker is running:
   Search logs for `OutboxProcessor` loop:
   ```text
   docker compose logs --tail=200 api | grep "OutboxProcessor"
   ```
2. Check for poison messages with high failure counts:
   ```sql
   SELECT id, event_type, attempts, error 
   FROM outbox_messages 
   WHERE processed_on_utc IS NULL AND attempts >= 3;
   ```
3. Check PostgreSQL table bloat and autovacuum on `outbox_messages`:
   ```sql
   SELECT relname, n_dead_tup, last_vacuum, last_autovacuum 
   FROM pg_stat_user_tables 
   WHERE relname = 'outbox_messages';
   ```

---

## 4. Containment & Mitigation
1. If broker latency is high, inspect RabbitMQ disk alarms and memory limits.
2. If `attempts >= 3` messages are stuck, investigate the specific exception (e.g. invalid serialization or schema drift).
3. If table bloat is high, execute manual vacuum:
   ```sql
   VACUUM ANALYZE outbox_messages;
   ```
4. Scale outbox workers if batch size is constrained:
   In `appsettings.json`:
   ```json
   "Outbox": {
     "BatchSize": 50,
     "IntervalMilliseconds": 1000
   }
   ```

---

## 5. Recovery & Verification
1. Confirm oldest pending message age drops below 5 seconds.
2. Confirm pending count stabilizes near 0 under normal traffic.
