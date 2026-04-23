---
id: D008
chosen_option: "Two-pass commit: parents (Pass 1) → children (Pass 2) in same per-batch TX"
problem_id: P008
tags: [ef-core, etl, transaction, batch-processing, fk-constraint, dotnet, postgresql]
related_snippets: [S008]
---

# Decision: Two-Pass FK-Safe Batch Commit (Parents → Children, Per-Batch TX)

## Context

`OrderOutboundItemTb` and `OrderOutboundActivityTb` have FK references to `OrderOutboundTb.Id` (DB-generated IDENTITY). Children cannot be inserted before parents because the FK ID is unknown until the parent row is committed. Previous code called `SaveChangesAsync()` inside `foreach` — one write per header, N writes per batch — and held the transaction outside the loop across the entire job.

## Options Considered

1. **Insert parents with temp IDs, update children post-commit** — requires a second round-trip to re-fetch IDs; complex.
2. **SaveChangesAsync() per header inside foreach** — N writes per batch; transaction held across entire job duration.
3. **Two-pass within per-batch TX: bulk-save parents → EF populates IDs → bulk-save children** — single TX per batch, minimal round-trips, FK safety via EF ID population after Pass 1 SaveChanges.

## Decision

Within each batch's `BeginTransactionAsync()` / `CommitAsync()` scope:
- **Pass 1**: Collect all header entities, call `SaveChangesAsync()` once. EF Core populates each entity's `.Id` from the DB-generated IDENTITY.
- **Pass 2**: Build child entities using the now-populated parent `.Id` values, call `SaveChangesAsync()` once for all children.
- Commit the transaction. Call `ChangeTracker.Clear()`.

## Consequences

- FK constraint satisfied without extra round-trips or temp IDs.
- Batch writes reduced from N×BatchSize (one per header) to 2 per batch (one for parents, one for children).
- Transaction hold = single batch duration, not full job duration.
- Pattern generalizes to any parent-child ETL with DB-generated PKs.
