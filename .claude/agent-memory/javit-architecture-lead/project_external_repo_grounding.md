---
name: external-repo-grounding-for-consultations
description: How to ground a consultation in a real external codebase (e.g. Sprint-OMS) when the inbox problem file points at source outside this repo, and how to handle credentials found while doing so
type: project
---

Some inbox problems (e.g. `inbox/oms/req.md`, 2026-07-22) point at a real external codebase
(`D:\workspace\Sprint-OMS`) rather than describing a self-contained synthetic problem. In these
cases problem-analyst-role grounding must actually read that source tree before writing the
problem JSON — do not invent architecture facts.

**Why:** A prior AI-assisted repo audit already exists inside that external repo at
`Sprint-OMS/output/review-architech.md` (dated 2026-07-09) — it is the direct source document
behind KB P018/D023. Any new consultation touching Sprint-OMS should check for this file (and
similar docs under `Sprint-OMS/docs/`) first, since it may already contain verified facts (tech
stack, DB topology, gaps) that would otherwise require a slow from-scratch audit, and skipping it
risks contradicting or duplicating a decision that document already fed into the KB.

**How to apply:**
- A recursive Glob/grep over the full `Sprint-OMS` root reliably times out (20s ripgrep limit) —
  it contains `node_modules`, `bin`, `obj`, `cypress` etc. Always scope Glob/Grep to a specific
  subfolder (`Order/`, `Master/`, `Portal/`, `Shared/`, `Front/`) or use plain `ls` via Bash on one
  directory at a time instead of a broad recursive pattern.
- Check `appsettings.*.json` and `DependencyInjection.cs` files per service for the real DB
  provider, connection string keys, and feature toggles (e.g. this repo's Redis cache is wired in
  code but disabled via `CacheSetting:Redis:Enabled=false` in dev) — these are exactly the kind of
  fact that changes an architectural recommendation and is easy to get wrong by assumption.
- `appsettings.Development.json` / `appsettings.AzureDevelop.json` in Sprint-OMS contain real
  plaintext credentials (DB passwords, Redis password, Teams webhook URL, JWT public key). Never
  reproduce the actual secret values in KB records or in the final report to the user — reference
  the config key/host pattern only (e.g. "single shared Postgres host, per dev appsettings") and
  redact the literal password/URL even when quoting the surrounding config for grounding.
- EF Core migration filenames/history are a fast way to infer schema evolution timeline and recent
  feature focus (e.g. `AddBuCodeToBookingOrders` migration directly confirmed BU is a first-class
  but only recently-added column) — check `*/Migrations/*.Designer.cs` filenames before reading
  full migration bodies.
