---
id: S011
slug: snowflake-id-generator
language: go
when_to_use: "Use when distributed services must generate globally unique IDs without a DB round-trip, and the IDs are used as sort keys or pagination cursors (time-sortable property). One Snowflake instance per machine — machine_id must be unique across the cluster (assign via Redis INCR or config)."
related_problems: []
related_decisions: [D014]
source: TA1
---

# Snowflake ID Generator (Go)

64-bit time-sortable ID generator. Bit layout: `[timestamp 40b][machine_id 12b][sequence 12b]`. Generates up to 4096 IDs per millisecond per machine. Uses a mutex to be goroutine-safe.

## ID structure

```
bits:  [42 timestamp][10 machine_id][12 sequence]
```

- Timestamp: milliseconds since custom epoch (reduces ID size vs Unix epoch)
- Machine ID: unique per node, max 1024 nodes (10-bit)
- Sequence: resets each millisecond, wraps at 4096

## When NOT to use

- Security-sensitive tokens (session keys, API keys) — Snowflake IDs expose approximate creation time
- Team does not control worker ID assignment — use UUID v4 instead
- Need IDs across more than 1024 nodes without coordination infrastructure
