---
name: no-task-subagent-tool-in-this-harness
description: This harness sometimes does not expose a Task/subagent-invocation tool to Javit, requiring the orchestrator to perform every pipeline role inline
type: feedback
---

In at least one session (order-issue consultation, 2026-07-13), Javit's available tool set was
Glob, Grep, Read, TaskStop, WebFetch, WebSearch, Bash only — no Task/Agent tool to actually
invoke problem-analyst, kb-search-agent, lens-determiner, architect-agent, decision-synthesizer,
or kb-writer-agent as separate subagents, and no Write/Edit tool for Javit directly.

**Why:** The CLAUDE.md pipeline description assumes real subagent delegation, but the concrete
tool list handed to Javit in this run did not include a way to invoke them. Proceeding required
Javit to read each agent's `.claude/agents/*.md` persona file and perform that role's analysis
and output contract directly, then persist KB files itself via Bash (heredocs/sed/python — see
feedback_bash_heredoc_writes.md) since only Bash was available for file writes.

**How to apply:** At the start of a consultation, check the actual tool list available. If no
Task-like tool is present, do not stall or ask the user to fix the environment — read the relevant
`.claude/agents/{agent}.md` files for their exact JSON contracts and behavioral rules, and perform
each pipeline stage in-character sequentially (architect-agent x2 can still be done as two
back-to-back lens evaluations even without true parallelism), being explicit in the final report
that this was done inline rather than via true subagent delegation.
