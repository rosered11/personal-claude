---
id: D006
chosen_option: "Mandatory 6-point clone verification checklist before deploy"
problem_id: P006
tags: [etl, copy-paste, correctness, testing, silent-failure, dotnet, process]
related_snippets: []
---

# Decision: ETL Clone Verification Checklist Mandatory Before Deploy

## Context

`barcode.cs` was cloned from `product.cs`. `GetProductStaging()` was updated to `SpcJdaBarcodeStaging` but `CheckPendingAsync()` retained the original `SpcJdaProductStaging` DbSet. The compiler raised no error. The job ran without exception but committed 0 records — a silent wrong-table query that required data analysis to detect.

## Options Considered

1. **No process change** — rely on code review; the reviewer missed the same bug that the author missed.
2. **Automated test per DbSet reference** — ideal but requires test infrastructure that does not currently exist for ETL services.
3. **Mandatory 6-point checklist enforced in PR description** — zero infrastructure cost; catches all 6 independent call sites that must be updated when cloning.

## Decision

Require the following checklist in the PR description for every cloned ETL service:
1. `CheckPendingAsync()` DbSet reference updated
2. `GetDataStaging()` DbSet reference updated
3. `UpsertAsync()` target table updated
4. `MarkProcessedAsync()` status field updated
5. DAG task ID string updated (no collision with source service)
6. Airflow variable keys updated

A PR reviewer must confirm all 6 boxes are checked before approving.

## Consequences

- Silent wrong-table bugs are caught before deploy, not discovered through data analysis after the fact.
- Zero tooling cost — enforced via PR template.
- Does not prevent future structural divergence between clones — this is a process control, not an architectural fix.
- Long-term fix is a generic base class that accepts DbSet<T> as a type parameter, eliminating copy-paste entirely.
