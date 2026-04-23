# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Multi-agent Architecture Consultant Team. Drop a Markdown problem file into `inbox/`, run `consult.py`, and a pipeline of Claude agents analyzes the problem from multiple architectural lenses, makes a decision, and stores everything in `knowledge-base/`.

## Development Commands

```bash
pip install -r requirements.txt       # install dependencies (anthropic, python-frontmatter)
python consult.py inbox/example-problem.md   # process one file
python consult.py --all               # process all .md files in inbox/
```

Requires `ANTHROPIC_API_KEY` in the environment.

## Architecture

```
consult.py
  └─► Orchestrator (agents/orchestrator.py)
        ├─► ProblemAnalystAgent    → structured problem JSON + tags
        ├─► KBSearchAgent          → finds related KB entries by tag overlap
        ├─► LensDeterminerAgent    → picks 2 contrasting architectural lenses per problem
        ├─► ArchitectAgent (×2)    → parallel; each evaluates problem through one lens
        ├─► DecisionSynthesizerAgent → picks best option, extracts code snippet
        └─► KBWriterAgent          → writes P-xxx.md, D-xxx.md, S-xxx/ to knowledge-base/
```

All agents extend `BaseAgent` (`agents/base.py`), which holds the shared Anthropic client, uses `claude-opus-4-7` with adaptive thinking, applies prompt caching (`cache_control: ephemeral`) on all system prompts, and provides `_call_json()` for structured JSON responses.

### Knowledge Base Layout

```
knowledge-base/
  index.md                          ← auto-updated table of all records
  problems/P{NNN}-{slug}.md         ← YAML frontmatter: id, title, date, tags, related_decisions, related_snippets
  decisions/D{NNN}-{slug}.md        ← YAML frontmatter: id, chosen_option, problem_id, tags, related_snippets
  snippets/S{NNN}-{slug}/
    context.md                      ← YAML frontmatter: when_to_use, related_problems, related_decisions
    code.{ext}                      ← raw code file
```

IDs are zero-padded three-digit sequences (`P001`, `D001`, `S001`). KB search uses tag-intersection scoring — no embeddings.

### Inbox

Drop `.md` files into `inbox/` describing the problem. Include code blocks inline if you have problematic code. The system determines which architectural lenses to apply automatically based on the problem content.
