# Memory Index — architect-agent

- [Stack Context](stack_context.md) — Primary tech stack; default code language by problem type (C#/SQL/Python/Go/Lua)
- [EF Core Patterns](ef_core_patterns.md) — IDbContextFactory, CompileQuery, AsNoTracking, ChangeTracker.Clear(), WHERE IN
- [ETL Patterns](etl_patterns.md) — Per-batch TX, BatchSize 10K, read-before-TX, two-pass FK commit, Polly retry
- [PostgreSQL Internals](postgresql_internals.md) — MVCC dead tuples, autovacuum scale_factor trap, REINDEX CONCURRENTLY
- [Sprint-OMS Deployment Context](sprint_oms_context.md) — TKE deployment, per-client self-signed cert bypass, no Polly, plaintext secrets confirmed
- [Lens-to-Tag Fit Patterns](lens_tag_patterns.md) — When Service Mesh lens fits, its scope limits, and rollout-safety framing
- [Hexagonal .NET Distributed Monolith](hexagonal_dotnet_distributed_monolith.md) — verify ProjectReferences directly; Contracts-assembly extraction pattern for Sprint-OMS-style audits
- [Secrets as Port](secrets_as_port.md) — model committed plaintext secrets as ISecretProvider port+adapter, not just a rotation bullet
- [Warehouse/PTL Context](warehouse_ptl_context.md) — CMG Put-to-Light domain facts, hard invariants, and why they push toward Saga over pure choreography; stack-default caveat.
- [RFID Event Platform Context](rfid_context.md) — SCM IT RFID platform domain facts, existing design principles (cache-only, no sync registry calls), hard invariants that push gate-verification problems toward a DDD aggregate.
