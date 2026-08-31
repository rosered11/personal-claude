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

**Update (2026-08-24)**: `python3` in this environment resolves to a native Windows
`python.exe` via a pyenv-win shim (`which python3` -> `.../pyenv-win/shims/python3`),
not an MSYS-aware binary. MSYS/Git-Bash only auto-translates `/d/...`-style paths for
values that look like direct command-line arguments to a Windows executable -- a
`/d/...` path embedded inside a Python string literal (e.g. `open("/d/workspace/...")`
inside a heredoc passed to `python3 -`) is passed through unmodified and will raise
`FileNotFoundError`, even though the identical path works fine for `cat`/`ls`/`mv` in
the same Bash session. Confirmed by a direct `python3 -c "os.path.exists('/d/...')"`
returning `False` for a file `ls` could see seconds earlier at the same absolute path.

**Why:** wasted a tool call chaining a `cat > scratch-file <<EOF` and a `python3 -
<<PYEOF` (which read that scratch file by its `/d/...` path) inside one Bash
invocation -- the python3 step silently couldn't find the file it had just been handed,
even though the file existed on disk by the time the command finished.

**How to apply:** when a Python heredoc script needs to read/write a file, either (a)
`cd` into the target directory first and use a bare relative filename (this already
works -- confirmed via the `index.md`/`architecture-transition.md` edits in this same
session, since `cd` sets the process's real Windows working directory, which `python3`
inherits correctly), or (b) `cp` the file into the current working directory first,
then reference it by relative name. Never pass a `/d/...`-style absolute path directly
into a Python string in this environment.
