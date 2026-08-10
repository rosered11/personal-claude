---
name: Snippet Tag Inheritance Convention
description: Snippets have no native "tags" field in context.md frontmatter — how to score them for tag-overlap search anyway
type: project
---

# Snippets have no `tags` field — inherit from linked problems/decisions

Checked all `knowledge-base/snippets/*/context.md` files (S001–S029, 25 total as of
2026-07-31): none carry a `tags` key. Frontmatter is only `id`/`slug` (sometimes),
`language`, `when_to_use`, `related_problems`, `related_decisions`, `source`. This is
consistent across the whole KB, not a one-off malformed file — it's the actual schema
kb-writer-agent uses (confirmed against CLAUDE.md's documented snippet layout too).

**Why this matters:** if you literally apply "missing tags field → empty list → score 0"
per the base methodology, the `snippets` result category will *always* be empty, for
every query, forever. That defeats the purpose of returning top-3 snippets.

**Convention adopted:** for scoring purposes only, compute each snippet's *effective
tags* as the union of the `tags` arrays of every entry listed in its `related_problems`
and `related_decisions` (looked up from the already-parsed P/D frontmatter in the same
KB pass). Score against `query_tags` using the normal Jaccard-inspired formula with
`entry_tags` = that inherited union. Report the inherited union as the snippet's `tags`
field in the output, and the true intersection as `overlap_tags`.

**How to apply:** Always resolve snippet relevance this way unless/until the KB schema
is changed to add a native `tags` field to snippet context.md files. If a future snippet
*does* have a `tags` key of its own, prefer that field directly instead of inheriting.
