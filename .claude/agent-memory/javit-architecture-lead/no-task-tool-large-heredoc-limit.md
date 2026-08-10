---
name: no-task-tool-large-heredoc-limit
description: This environment gives Javit no Task/Agent-spawning tool and no Write/Edit tool -- must simulate the whole pipeline directly and write files via Bash heredocs, which fail silently past a certain single-command length.
type: feedback
---

In this harness, javit-architecture-lead's tool list is only Glob/Grep/Read/TaskStop/WebFetch/
WebSearch/Bash -- there is no Task/Agent tool to actually spawn problem-analyst, kb-search-agent,
lens-determiner, architect-agent, decision-synthesizer, kb-writer-agent, or career-mentor as
separate subagent contexts, and no Write/Edit tool either. In practice this means Javit must
personally perform every pipeline stage's analysis (grounded in real Read/Grep/Glob/Bash
exploration) and then persist the resulting P/D/S files and index.md/roadmap updates itself via
Bash `cat > file <<'EOF' ... EOF` heredocs.

A single very large `cat > file <<'EOF' ... EOF` command (roughly 90+ lines of heredoc body in one
Bash tool call) reliably fails with `unexpected EOF while looking for matching` even though the
heredoc content itself is syntactically valid -- confirmed by bisection, not a quoting/apostrophe
issue. The fix is to build the file across several smaller `cat >>`/append calls (each comfortably
under that size) and `mv` the assembled temp file into place at the end, always using forward-slash
paths (`/d/workspace/...`) rather than Windows backslash paths, since backslashes get silently
stripped/mangled when passed through this Bash tool.

**Why:** Wasted several failed tool calls before isolating this as a transport/length limit rather
than a content problem -- backslash paths and apostrophes were both wrongly suspected first.

**How to apply:** When writing any KB record, roadmap update, or other multi-paragraph file in this
environment, default to chunked `cat >>` appends into a `/tmp` (or scratchpad) file followed by a
final `mv`, rather than one large heredoc. Always use `/d/workspace/...` style paths in Bash, never
`D:\workspace\...`.
