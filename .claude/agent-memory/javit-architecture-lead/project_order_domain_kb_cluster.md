---
name: order-domain-kb-cluster
description: The Order/OMS domain is the most recurring problem cluster in this KB — tags and precedents to check first for any new order-related problem
type: project
---

Problems P001, P008, P010, P013-P015, P018, P019, P020 (and their D/S pairs) all touch the same
Order/OMS bounded context across different services (Order.API, validate-service/Validator.API,
the OMS modular monolith). Recurring tag cluster: `ef-core`, `mssql`/`postgresql`, `dotnet`,
`integration-events`, `idempotency`, `microservices`/`oms`.

**Why:** P019/D024 (2026-07-13, validate-service SQL timeout + duplicate-key on Kafka retry)
scored only 0.45 tag-overlap against the closest precedent P010/D015 (Order.API concurrent
running-number race + missing event-consumer idempotency) — below the 0.8 KB-writer UPDATE
threshold, so it correctly became a new CREATE-mode record — but the two are clearly related:
D015 established the `processed_events` idempotency-key pattern (via D012/S012) that D024 extends
to a second, previously-uncovered consumer (the MAO/Kafka ingestion path in validate-service).
P020/D025 (2026-07-22, OMS DB load risk from BU growth — real Sprint-OMS repo audit, not a
synthetic inbox problem) is the same pattern again: only 0.25 tag-overlap against P018 (closest
match) and 0.3 against D018, both well below the UPDATE threshold, yet clearly the same lineage —
it is a direct, deeper follow-on to P018/D023's open question #3 ("multi-tenancy/data isolation
requirements per BU undetermined") and to the same source document
(`output/review-architech.md` in the external Sprint-OMS repo) that originally produced P018.

**How to apply:** For any new Order/OMS-related problem, explicitly check P010/D015/S015,
P013-P015/D018-D020 (greenfield OMS design precedents), and P018/D023/S023 (service-boundary +
BFF/gateway/observability precedent, same repo audit lineage) even if tag-overlap scoring alone
doesn't surface them at the top — they encode idempotency, aggregate, outbox, and (as of P020)
multi-tenancy-data-scaling patterns this domain reuses constantly. Low literal tag-overlap between
two OMS problems does not mean they are unrelated — the OMS lineage keeps spawning narrowly-scoped
follow-on problems (service boundary → this one → next one) from the same original repo audit;
always read the actual problem content, not just the score, before concluding "no relevant
precedent." Also always cross-check whether a new OMS decision would contradict the standing D020
precedent (Modular Monolith over Microservices at 70K order-lines/day) — D025 deliberately designed
around this by keeping its chosen option (CQRS read-scaling) inside the single deployable and
framing the deferred DDD/tenant-partitioning option as data-partitioning, not service decomposition.

**2026-07-22 addendum (P021/D026):** activity-service (Activity.API, writes ProcessActivity /
ProcessActivityDependency keyed by SourceOrderId/SourceSubOrderId) is part of the same broader
order-processing EF Core/SQL Server write-path family as P010 and P019, even though its tags
(ef-core, mssql, command-timeout, batch-processing, retry-policy, integration-events, dotnet,
rabbitmq) only scored ~0.45 overlap against P019 (the closest match) -- again below the 0.8 UPDATE
threshold, again correctly a CREATE. Root cause here was distinct from both: P010 was a running-
number race + missing idempotency, P019 was a too-tight timeout + blind-INSERT duplicate-key, P021
was an unbounded batched MERGE (already idempotent) combined with a previously-unnoticed *stacked
retry policy* (EF Core's own EnableRetryOnFailure plus a hand-rolled app-level retry loop, capable
of resubmitting the same oversized command up to 9x). Confirms the established pattern: always check
P010/D015, P019/D024, and now P021/D026 for any new order/activity-domain EF Core write-path
problem, even at low tag-overlap scores -- this is a recurring problem family (EF Core write-path
reliability across SQL Server-backed order/activity services), not isolated incidents.

**2026-07-22 addendum (P022/D027):** P022 (SQL Server audit false-positive traced to the
TaskIndexRebuild ONLINE index-rebuild job on OrderDb) is adjacent to but structurally distinct from the
EF Core write-path family (P010/P019/P021) documented above -- it concerns the SQL Server *engine/DBA*
side of the same OrderDb (index maintenance, audit-trail attribution) rather than the *application*
write path (EF Core SaveChanges, retry policies, idempotency). Tag overlap against the whole KB topped
out at ~0.125 (shared `mssql` or `observability` tags only against P010/P018/P019/P021/D004/D015/D023/
D024/D026), correctly triggering CREATE mode -- this establishes a new sub-cluster (database-maintenance
/ audit-observability) worth checking for any future OrderDb-adjacent DBA/ops problem, distinct from the
EF Core write-path sub-cluster. Also the first purely diagnostic/forensic consultation in this KB (see
pattern_diagnostic_vs_design_consultations.md) rather than a design or remediation problem.


**2026-07-29 addendum (P023/D028/S028):** The very same stored procedure covered by P022/D027/S027
(`TaskIndexRebuild` on OrderDb) generated a *second*, structurally distinct problem within the same
week: P022 was about audit-trail attribution and unconditional rebuild volume; P023 is about the
rebuild job's execution-time load pacing causing SQL command timeouts on concurrent OLTP traffic.
Tag overlap of P023 against P022 was 0.5 (4 shared tags: mssql, index-rebuild, database-maintenance,
online-index-operation, out of 8 tags each) -- meaningfully higher than the ~0.125-0.45 overlaps seen
in the rest of this cluster, but still below the 0.8 UPDATE threshold, so CREATE was correct. This
establishes a reusable judgment rule: the same production object/procedure can accumulate multiple
independent architectural problems over time (selection-of-work vs. pacing-of-work, in this case),
and each deserves its own P/D/S record that explicitly extends (not silently contradicts or
duplicates) the prior one, rather than being folded into the earlier record just because the tag
overlap is elevated by sharing the same object. Also notable: this consultation required verifying
user-supplied "reference" files before use -- both `script-rebuild.sql` and `schema-database.sql`
(inbox/rebuild-index-db/) turned out to be byte-for-byte identical plain schema exports with zero
rebuild logic, so the user was asked how to proceed and explicitly chose to treat the KB-documented
`TaskIndexRebuild` (S027 state) as the real prior baseline instead. Lens pairing reused Layered
Architecture vs. Event-Driven Architecture from D027 -- justified here because the *role* each lens
played differed completely (in-procedure WAIT_AT_LOW_PRIORITY/pacing vs. audit-window event
publication in D027; in-procedure throttling vs. queue-dispatched worker decoupling in D028), so
reusing the pair was a deliberate re-application to a new facet of the same object, not institutional
repetition -- worth checking in `lens-determiner`'s KB-dedup notes for future TaskIndexRebuild-adjacent
problems.

**2026-07-29 addendum #2 (P023/D028/S028 revised via UPDATE, not a new record):** A second,
same-day inbox submission (`inbox/rebuild-index-db/`) supplied the real `script-rebuild.sql` that
the original P023 consultation could not obtain (see prior addendum). This confirmed P023's problem
byte-for-byte against P022's documented script and, critically, revealed that neither D027 nor D028
had actually been deployed -- only decided. Tag overlap against P023 scored ~0.89 (>= 0.8), so this
correctly triggered the KB-writer UPDATE path for the first time in this KB's history rather than
creating P024 -- see `pattern_decided_vs_deployed_kb_update.md` for the full writeup and the
generalizable "decided vs. deployed" verification lesson.
