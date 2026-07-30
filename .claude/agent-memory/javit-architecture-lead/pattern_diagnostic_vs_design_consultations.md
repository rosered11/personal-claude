---
name: diagnostic-forensic-vs-design-consultations
description: How to run the pipeline when the inbox problem is a forensic/diagnostic question ("is X caused by Y?") rather than a greenfield or remediation design question
type: project
---

P022/D027/S027 (2026-07-22, inbox/database-index.md) was the first consultation in this KB that was
purely diagnostic: the user asked whether a suspicious SQL Server audit-logged query
(`insert [dbo].[SubOrderItem] select * from [dbo].[SubOrderItem] with (index = 1)`) was caused by a
scheduled `TaskIndexRebuild` stored procedure (ALTER INDEX ... REBUILD WITH ONLINE=ON across ~190
indexes, day-of-week branched). There was no existing "broken" architecture to redesign -- the answer
to the literal question was "very likely yes, this is SQL Server's own internal engine-generated DML
from the ONLINE clustered-index rebuild, not a security incident" (WITH (INDEX = 1) is the legacy
numeric hint that always resolves to the clustered index; SubOrderItem's clustered index PK_SubOrderItem
is rebuilt on the @day=3 branch, and the audited statement's table name and timing both match).

**Why:** problem-analyst's contract explicitly supports "root_cause: Undetermined with ranked hypotheses"
for exactly this situation -- the honest, useful output for a forensic question is a confidence-ranked
hypothesis list plus a named, cheap verification step (here: compare the audited row's session_id/
login_name/application_name against the SQL Agent job session for TaskIndexRebuild), not a false
assertion of certainty. The *architectural* problem worth running the two-lens pipeline on was one layer
up from the literal question: the audit trail has no mechanism to correlate engine-generated DML back to
a known maintenance job, and the maintenance job itself unconditionally rebuilds indexes regardless of
fragmentation, which is the actual root contributor to audit-noise volume. Reading the full 267-line
script directly (not just skimming) also surfaced a concrete secondary defect (PK_StoreLocation
redundantly rebuilt on both @day=4 and @day=6) that added credibility and fed directly into the chosen
fix (config-driven schedule that makes that class of duplication a constraint violation instead of a
silent bug).

**How to apply:** When an inbox problem is phrased as a yes/no or "is X the cause of Y" question:
1. Answer the literal forensic question first and explicitly, with ranked hypotheses and a named
   verification step if full certainty isn't possible from the given evidence alone -- do not force a
   false design-tradeoff framing onto a question that has a mostly-determinable factual answer.
2. Then identify the *adjacent* architectural gap that made the question hard to answer in the first
   place (here: missing audit/maintenance-job correlation + unconditional rebuild-noise volume) and run
   the normal two-lens pipeline (problem-analyst -> kb-search -> lens-determiner -> 2x architect ->
   decision-synthesizer -> kb-writer -> career-mentor) against *that* gap, not the forensic question
   itself.
3. Read any attached script/code in full before writing the problem record -- skimming would have
   missed the PK_StoreLocation duplicate, which became a concrete, credibility-adding data point in both
   the root_cause section and the chosen decision's rationale.
