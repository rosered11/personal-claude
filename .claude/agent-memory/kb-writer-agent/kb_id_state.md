---
name: KB ID State
description: Current highest allocated IDs in the knowledge base — used to determine next sequential ID when creating new records
type: project
---

# KB ID State — Seeded 2026-04-23

## Current highest IDs

| Type | Highest Allocated | Next Available |
|------|-----------------|----------------|
| Problems | P009 | P010 |
| Decisions | D014 | D015 |
| Snippets | S014 | S015 |

## Notes

- S004, S006, S007, S013 are intentionally skipped (gaps preserved to maintain NNN alignment between P/D/S from the same pipeline run)
- When a new pipeline run produces P010, it should also produce D015 and S015 (same NNN)
- Always zero-pad to 3 digits: P010, not P10

**Why:** Highest seeded ID tracks what was written during the initial KOS-to-KB seeding. kb-writer-agent must read this before allocating IDs for new pipeline runs to avoid collisions.

**How to apply:** Before writing any new P/D/S record, check existing files in `knowledge-base/` with `Glob knowledge-base/**/*.md`, find the highest existing ID, and allocate the next one. This memory record is a snapshot — always verify against actual files on disk.
