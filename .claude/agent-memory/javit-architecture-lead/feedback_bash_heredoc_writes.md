---
name: bash-heredoc-file-writes-in-this-environment
description: How to reliably write/append multi-line KB markdown and code files via the Bash tool in this Windows Git-Bash environment
type: feedback
---

Long single `cat <<'EOF' > file` heredocs (roughly 90-130+ lines of prose-heavy markdown,
even with a quoted delimiter so quoting should not matter) intermittently fail in this
environment with `unexpected EOF while looking for matching` even though the heredoc syntax
is valid bash. `python3 -c "..."` inline execution also silently produced no stdout in this
same session (likely a Python 3.15 alpha/build quirk here), so don't rely on it for quick
checks.

**Why:** Observed directly while writing D024 (a ~140-line decision record) for the
order-issue consultation on 2026-07-13 — the same heredoc content failed twice, then
succeeded once split into ~20-40 line chunks appended sequentially (`>` then multiple `>>`).
Text replacement/insertion into existing files worked reliably via `python - <<'PYEOF' ... PYEOF`
(heredoc piped to `python -`, not `python3 -c`), using plain string replace on an exact-match
marker read from the file first.

**How to apply:** When writing a new KB record (P/D/S files) or any file with more than ~50
lines via Bash, write it in sequential chunks (`cat <<'EOF' > file` for the first chunk, then
`cat <<'EOF' >> file` for subsequent chunks) rather than one large heredoc. For editing/inserting
into an *existing* file (e.g. index.md, roadmap files), prefer `sed -i 'LINEa\...'` for single-line
row insertions, or `python - <<'PYEOF'` with `open()/read()/replace()/write()` for anything
involving exact-text matching or multi-line block insertion — do not use `python3 -c` for output
you need to see.
