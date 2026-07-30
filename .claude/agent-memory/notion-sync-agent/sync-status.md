---
name: Sync Status Log
description: Tracks each sync run — counts of created/updated/skipped records and any notable observations
type: project
---

## 2026-06-17

- Problems: 16 total (1 created: P016, 15 skipped)
- Decisions: 21 total (1 created: D021, 20 skipped)
- Snippets: 17 total (1 created: S021, 16 skipped)
- Relations: all P<->D, P<->S, D<->S links applied (54 pages processed)
- Script: sync/notion_kb_sync.py (full sync, no flags)
- Config: sync/notion_kb_config.json present, all 3 DB IDs confirmed valid
- Token: NOTION_TOKEN from sync/.env — format ntn_xxx (not secret_xxx — still accepted by API)
- No errors or rate-limit hits observed

## 2026-07-22

- Problems: 20 total (4 created: P017, P018, P019, P020; 16 skipped)
- Decisions: 25 total (4 created: D022, D023, D024, D025; 21 skipped)
- Snippets: 25 total (4 created: S022, S023, S024, S025; 21 skipped)
- Relations: all P<->D, P<->S, D<->S links applied (66 pages processed total)
- Script: sync/notion_kb_sync.py (full sync, no flags) — no --db filter, no --rebuild-body
- Config: sync/notion_kb_config.json unchanged, all 3 DB IDs still valid
- Token: sync/.env NOTION_TOKEN + NOTION_PARENT_PAGE_ID both present, no prompts needed
- Backlog note: run picked up 4 unsynced records per DB beyond the requested P020/D025/S025
  (P017-P019, D022-D024, S022-S024 were also pending from earlier KB writes not yet synced)
- No errors or rate-limit hits observed; full sync completed in one pass without retries

## 2026-07-22 (incremental, --rebuild-body)

- Task: P020 and D025 KB files were edited (added confirmed ~1M records/day write-volume
  figure). Ran `python sync/notion_kb_sync.py --rebuild-body` for a full sync + targeted
  body refresh of only the changed records (script auto-detects changed files via
  sync/notion_kb_hashes.json — --rebuild-body only re-renders bodies where the file hash
  differs from last sync, so this is safe/cheap even as a full-sync flag).
- **BUG FOUND AND FIXED**: `NotionClient.append_children()` in sync/notion_kb_sync.py used
  HTTP `POST` for `/blocks/{block_id}/children`, but Notion's "Append block children" API
  requires `PATCH`. Every past `--rebuild-body` attempt would have silently failed with
  `400 invalid_request_url`, masked by the script's own error handler which misattributes
  the failure to a missing "Update content" integration capability (see error text at
  upsert_page(): "Enable 'Update content' in your Notion integration settings" — this
  message is WRONG/misleading, the real cause was the wrong HTTP verb). Fixed at
  sync/notion_kb_sync.py:526 (POST -> PATCH). Confirmed via direct curl test against the
  live API that POST returns invalid_request_url and PATCH succeeds on the same URL/body.
  If you see "Body update failed... invalid_request_url" again in future runs, this fix
  should already be in place — if it recurs, suspect a regression of this same line.
- After the fix, re-ran --rebuild-body: P020 and D025 both show `[rebuilt]`, all other 63
  records `[skipped]` (hash unchanged). Verified live via Notion API (GET block children)
  that both pages' bodies now contain the new ~1M records/day paragraphs — not just a
  properties-only update.
- Caution: while diagnosing with raw curl/requests calls outside the script, a stray test
  paragraph block was briefly created on the live P001 page. It was immediately deleted via
  `DELETE /v1/blocks/{id}`. Lesson: when testing the Notion API directly for debugging, use
  a scratch/throwaway page if possible, or clean up immediately (as done here) — the script
  itself never touched P001.
- Total this run: 66 pages processed, 2 bodies rebuilt (P020, D025), full relations pass
  completed (P<->D, P<->S, D<->S), no rate-limit hits.
