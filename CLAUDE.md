# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A multi-agent Architecture Consultant system built entirely as Claude Code subagents. Drop a Markdown problem file into `inbox/`, ask Javit to process it, and a pipeline of six agents analyzes the problem from two contrasting architectural lenses, makes a decision, and stores everything in `knowledge-base/`.

The consultation pipeline runs entirely as Claude Code agents with no build step. A separate Notion sync utility (`sync/notion_kb_sync.py`) can push KB records to Notion on demand.

## How to Run a Consultation

Describe an architecture problem or point to a file in `inbox/` and invoke the `javit-architecture-lead` agent:

> "Process inbox/my-problem.md"

Javit will orchestrate the full pipeline. The pipeline makes 6 Claude API calls (2 in parallel for the architect evaluations).

## Agent Architecture

All agents live in `.claude/agents/`. The pipeline flows:

```
javit-architecture-lead  (orchestrator)
  ├── problem-analyst        → structured problem JSON + tags
  ├── kb-search-agent        → top-3 related KB entries by tag overlap (no LLM)
  ├── lens-determiner        → picks 2 contrasting lenses; returns lens_a, lens_b + justifications
  ├── architect-agent (×2)   → parallel; each evaluates the problem through one lens
  ├── decision-synthesizer   → picks the best option, blends insights, final code snippet
  └── kb-writer-agent        → writes P-xxx.md, D-xxx.md, S-xxx/ and updates index.md (no LLM)
```

ArchitectAgents are always launched in parallel — never sequentially.

### Notion Sync (standalone, not part of the consultation pipeline)

```
notion-sync-agent  (invoke manually after KB changes)
  └── sync/notion_kb_sync.py  → upserts P/D/S pages to 3 linked Notion databases
                                 pass 1: create/update pages with properties + body
                                 pass 2: link all cross-relations (P↔D, P↔S, D↔S)
```

Invoke: `"sync the knowledge base to Notion"` — the agent checks prerequisites, runs the script, and reports what was created/updated.

### Agent-to-Agent Contract

- `problem-analyst` → outputs a JSON object with: `title`, `problem`, `root_cause`, `summary`, `context`, `constraints`, `tags`, `severity`, `affected_components`
- `lens-determiner` → receives problem JSON + KB search results; outputs JSON with: `lens_a`, `lens_b`, `lens_a_justification`, `lens_b_justification`, `contrast_rationale`, `kb_influence`
- `architect-agent` → receives problem JSON + a lens name; outputs JSON with: `lens`, `option_title`, `pros`, `cons`, `rationale`, `complexity`, `code_snippet`
- `decision-synthesizer` → receives both architect JSONs; outputs JSON with: `chosen_option`, `blended_rationale`, `rejected_options`, `code_snippet`, `confidence`
- `kb-writer-agent` → receives all of the above including `kb_search_results`; if top problem match `overlap_score >= 0.8` → UPDATE existing P/D/S records in place; otherwise → CREATE new records with next sequential IDs

## Knowledge Base Layout

```
knowledge-base/
  index.md                              ← auto-updated master table
  problems/P{NNN}-{slug}.md            ← YAML frontmatter: id, title, date, tags, related_decisions, related_snippets
  decisions/D{NNN}-{slug}.md           ← YAML frontmatter: id, chosen_option, problem_id, tags, related_snippets
  snippets/S{NNN}-{slug}/
    context.md                          ← YAML frontmatter: when_to_use, related_problems, related_decisions
    code.{ext}                          ← raw code file
```

IDs are zero-padded three-digit sequences (`P001`, `D001`, `S001`). Matching NNN values across P/D/S from the same pipeline run. KB search uses Jaccard-inspired tag-intersection scoring — no embeddings.

## Agent Memory

Each agent has an isolated persistent memory directory:

```
.claude/agent-memory/
  javit-architecture-lead/
  problem-analyst/
  lens-determiner/
  architect-agent/
  decision-synthesizer/
  kb-search-agent/
  kb-writer-agent/
  notion-sync-agent/
```

Memory files use YAML frontmatter (`name`, `description`, `type`) and each agent maintains a `MEMORY.md` index. Memory scope is project-level (shared via version control). Memory types: `user`, `feedback`, `project`, `reference`.

## Lens Pool

`lens-determiner` selects 2 contrasting lenses per problem from: Event-Driven Architecture, CQRS, Hexagonal Architecture, Microservices, Domain-Driven Design, Serverless, Saga Pattern, Strangler Fig, Layered Architecture, Service Mesh, and others. Architect instances are forbidden from drifting outside their assigned lens.
