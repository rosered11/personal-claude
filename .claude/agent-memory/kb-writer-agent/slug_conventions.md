---
name: Slug Conventions
description: Rules for generating file slugs for KB records — lowercase hyphenated, technology-first, max 5-6 words
type: feedback
---

# Slug Conventions for KB Record Filenames

## Rule

Slugs are lowercase hyphenated, max 5–6 words, technology-first (e.g., `ef-core-n1-pool-exhaustion`, not `pool-exhaustion-ef-core`).

**Why:** Technology-first slugs group related files together alphabetically in filesystem listings. Shorter slugs are more readable in `index.md` table rows and in agent tool calls.

**How to apply:** When kb-writer-agent writes a new P/D/S file:
1. Identify the primary technology or system involved
2. Identify the problem/decision type in 2–3 words
3. Combine: `{technology}-{problem-type}`
4. Max 6 words total; abbreviate when obvious (n+1 → n1, changetracker → changetracker, autovacuum → autovacuum)

## Examples from seeded KB

| Good | Bad |
|------|-----|
| `ef-core-n1-pool-exhaustion` | `pool-exhaustion-caused-by-n1-queries` |
| `postgresql-autovacuum-index-bloat` | `index-bloat-from-autovacuum-not-running` |
| `etl-per-batch-transaction-scope` | `transaction-scope-for-etl-batch-loops` |
| `subprocess-hard-timeout-daemon` | `airflow-subprocess-hard-timeout-using-daemon-thread` |
| `two-pass-fk-safe-batch-commit` | `fk-safe-two-pass-commit-for-etl` |
| `redis-token-bucket-rate-limiter` | `token-bucket-rate-limiting-using-redis-lua` |

## Special cases

- Technology abbreviations: `ef-core` not `entity-framework-core`, `postgresql` not `postgres`, `airflow` not `apache-airflow`
- Problem abbreviations: `n1` for N+1, `oom` for out-of-memory, `tx` for transaction (only in decision titles, not slugs)
- For standalone decisions (no problem): slug describes the decision type, not a specific incident
