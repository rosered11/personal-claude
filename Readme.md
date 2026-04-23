# Readme

claude --resume "multi-agent-architect-consultant"


6 agent classes, 7 runs per pipeline (ArchitectAgent runs twice in parallel).                           
                                                                                                          
  ---                                                                                                     
  Agents that call Claude API                                                                             
   
  ┌─────┬──────────────────────────┬───────────────────────────────────────────────────────────────────┐  
  │  #  │          Agent           │                               Role                                │
  ├─────┼──────────────────────────┼───────────────────────────────────────────────────────────────────┤
  │ 1   │ ProblemAnalystAgent      │ Reads your Markdown problem file → extracts structured JSON:      │
  │     │                          │ title, summary, context, constraints, tags                        │
  ├─────┼──────────────────────────┼───────────────────────────────────────────────────────────────────┤
  │ 2   │ LensDeterminerAgent      │ Reads the problem JSON → picks 2 contrasting architectural lenses │
  │     │                          │  from a pool of 7 (e.g. consistency-first vs operability-first)   │
  ├─────┼──────────────────────────┼───────────────────────────────────────────────────────────────────┤
  │ 3   │ ArchitectAgent (Lens A)  │ Evaluates the problem strictly through Lens A → proposes an       │
  │     │                          │ option with pros/cons/rationale/code snippet                      │
  ├─────┼──────────────────────────┼───────────────────────────────────────────────────────────────────┤
  │ 4   │ ArchitectAgent (Lens B)  │ Same as above but through Lens B — runs in parallel with Agent 3  │
  ├─────┼──────────────────────────┼───────────────────────────────────────────────────────────────────┤
  │     │                          │ Receives both architect analyses → picks the best option, blends  │
  │ 5   │ DecisionSynthesizerAgent │ insights, explains what was rejected and why, produces final code │
  │     │                          │  snippet                                                          │
  └─────┴──────────────────────────┴───────────────────────────────────────────────────────────────────┘

  Agents that do NOT call Claude API

  ┌─────┬───────────────┬──────────────────────────────────────────────────────────────────────────────┐
  │  #  │     Agent     │                                     Role                                     │
  ├─────┼───────────────┼──────────────────────────────────────────────────────────────────────────────┤
  │ 6   │ KBSearchAgent │ Reads existing knowledge-base/ files → scores them by tag overlap → returns  │
  │     │               │ top 3 related problems/decisions/snippets (pure Python, no LLM)              │
  ├─────┼───────────────┼──────────────────────────────────────────────────────────────────────────────┤
  │ 7   │ KBWriterAgent │ Writes P{NNN}.md, D{NNN}.md, S{NNN}/ to disk → updates index.md (pure        │
  │     │               │ Python, no LLM)                                                              │
  └─────┴───────────────┴──────────────────────────────────────────────────────────────────────────────┘

  ---
  Flow in one line per agent

  Your problem.md
    → [1] Analyst      — "what is this problem?"
    → [6] KB Search    — "what do we already know?"
    → [2] Lens Picker  — "which 2 angles to evaluate from?"
    → [3] Architect A  ─┐
    → [4] Architect B  ─┘ parallel — "what does each expert recommend?"
    → [5] Synthesizer  — "which option wins and why?"
    → [7] KB Writer    — "save everything to knowledge-base/"

  5 Claude API calls per run (2 of them in parallel). Total cost per problem: ~5 Opus 4.7 calls with
  adaptive thinking.