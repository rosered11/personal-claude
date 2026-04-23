---
id: D014
chosen_option: "Snowflake ID for distributed time-sortable; UUID v4 for fully random"
tags: [id-generation, snowflake, uuid, distributed, scalability, go, architecture]
related_snippets: [S011]
---

# Decision: Distributed ID Generation — Snowflake vs UUID v4

## Context

Distributed services cannot use DB-generated IDENTITY columns for IDs when records are created across multiple nodes before being written to a central store. IDs must be unique without coordination. Secondary requirement: IDs used as sort keys (event logs, time-series records) benefit from time-sortable generation.

## Options Considered

1. **DB IDENTITY / SERIAL** — simple; requires a DB round-trip before the record can be referenced; not viable for pre-generation.
2. **UUID v4** — globally unique, no coordination; not time-sortable; 128-bit; index fragmentation under sequential inserts.
3. **Snowflake ID (64-bit)** — timestamp + worker ID + sequence; time-sortable; 64-bit (fits BIGINT); no coordination beyond worker ID assignment.
4. **ULID** — like UUID v4 but lexicographically sortable; 128-bit; less ecosystem support.

## Decision

Use **Snowflake ID** (see S011, Go implementation) for any entity where:
- Records are created on distributed nodes before DB write, or
- The ID is used as a sort key or cursor in pagination.

Use **UUID v4** for entities where:
- Randomness is a security property (session tokens, API keys), or
- The team does not control worker ID assignment infrastructure.

## Consequences

- Snowflake: requires worker ID assignment (typically via Redis incr or config); monotonically increasing within a millisecond → B-tree index stays sequential → fewer page splits.
- UUID v4: zero infrastructure; 128-bit → larger index; random → index fragmentation under high insert rate.
- Snowflake IDs expose approximate creation timestamp — acceptable for internal entities, unacceptable for security-sensitive tokens.
- Worker ID collision risk: must ensure no two nodes share the same worker ID (max 1024 workers in standard 10-bit Snowflake).
