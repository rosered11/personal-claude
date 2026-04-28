---
when_to_use: "Use this pattern for any single-writer background worker that polls a PostgreSQL outbox table. Applies when: (1) the worker must be single-instance but runs in a Kubernetes environment where accidental multi-replica deployment is possible, (2) Kubernetes rolling updates must not cause event delivery gaps, (3) outbox events must be delivered at-least-once with idempotent ACL adapter calls."
related_problems:
  - P015
related_decisions:
  - D020
  - D018
language: "C#"
---

# S020 — OMS Outbox Worker: FOR UPDATE SKIP LOCKED + Graceful Shutdown

## What This Solves

Two operational gaps in the OutboxWorker that the D020 Modular Monolith analysis identified:

1. **Duplicate event delivery**: If `oms-outbox-worker` is accidentally scaled to 2 replicas,
   both workers race to process the same outbox events. `FOR UPDATE SKIP LOCKED` prevents this:
   rows locked by Worker A are skipped by Worker B rather than processed twice.

2. **Event delivery gap on restart**: Kubernetes sends SIGTERM before terminating a pod.
   Without `CancellationToken` handling, the worker stops mid-batch and the in-flight events
   are not marked as processed — they will be re-delivered on the next worker start (safe, if
   ACL adapters are idempotent) but the gap period has zero delivery attempts. Graceful shutdown
   ensures the in-flight batch completes before the pod terminates.

## PostgreSQL Locking Behaviour

`FOR UPDATE SKIP LOCKED` acquires a row-level lock on each selected row. Any concurrent
SELECT with `FOR UPDATE SKIP LOCKED` skips locked rows rather than blocking. This means:
- Worker A locks rows 1-50; Worker B's query returns rows 51-100 (if any exist)
- If only 50 unprocessed rows exist, Worker B's query returns zero rows — it sleeps and retries
- No deadlocks; no blocking waits; no duplicate processing

## Kubernetes Configuration Required

```yaml
# oms-outbox-worker Deployment
spec:
  replicas: 1                           # single-writer constraint
  template:
    spec:
      terminationGracePeriodSeconds: 60 # allow in-flight batch to complete
      containers:
        - name: oms-outbox-worker
          env:
            - name: DOTNET_SHUTDOWNTIMEOUTSECONDS
              value: "55"               # slightly less than terminationGracePeriodSeconds
```

## Idempotency Requirement

All ACL adapter HTTP calls dispatched from the outbox worker must be idempotent:
```
X-Idempotency-Key: {outbox_event.Id}
```
This ensures that if the worker retries a failed HTTP call (or the pod restarts mid-batch),
the downstream system deduplicates the request rather than processing it twice.
