---
name: KB ID State
description: Current highest allocated IDs in the knowledge base — used to determine next sequential ID when creating new records
type: project
---

# KB ID State — Updated 2026-08-24 (P032/D037/S037 run)

## Current highest IDs

| Type | Highest Allocated | Next Available |
|------|-----------------|----------------|
| Problems | P032 | P033 |
| Decisions | D037 | D038 |
| Snippets | S037 | S038 |

## Notes

- S004, S006, S007, S013 are intentionally skipped (gaps preserved to maintain NNN alignment between P/D/S from the same pipeline run)
- P010-P032 / D015-D037 / S015-S037 (minus the skipped snippet gaps above) were all added in later sessions after this file's original 2026-04-23 seeding and are documented in `knowledge-base/index.md`'s trailing "_Also ..._" notes, not repeated here in detail
- When a new pipeline run produces P033, it should also produce D038 and S038 (same NNN)
- Always zero-pad to 3 digits: P033, not P33

**Why:** Highest seeded ID tracks what was written during the initial KOS-to-KB seeding, plus every subsequent pipeline run. kb-writer-agent must read this before allocating IDs for new pipeline runs to avoid collisions.

**How to apply:** Before writing any new P/D/S record, check existing files in `knowledge-base/` with `Glob knowledge-base/**/*.md` (or read `knowledge-base/index.md`'s tables directly, which is usually faster), find the highest existing ID, and allocate the next one. This memory record is a snapshot — always verify against actual files on disk, since it is not updated after every single consultation.
