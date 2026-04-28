# Readme


claude --resume 697d0038-30c8-436e-89b0-f21019633e46 -> OMS

## Sync Notion

python sync/notion_kb_sync.py

## Diagram

```
flowchart TD                                                                                                            
      User(["👤 User\n\"Process inbox/problem.md\""])                                                                     
      Javit["🎯 javit-architecture-lead\nOrchestrator"]                                                                   
      PA["📋 problem-analyst\nExtracts: title, tags,\nconstraints, root cause"]
      KB["🔍 kb-search-agent\nJaccard tag scoring\n→ top-3 related records"]
      LD["🔭 lens-determiner\nPicks 2 contrasting lenses\ne.g. CQRS vs Event-Driven"]
      A1["🏛️  architect-agent A\nEvaluates through Lens A\n→ pros, cons, code snippet"]
      A2["🏛️  architect-agent B\nEvaluates through Lens B\n→ pros, cons, code snippet"]
      DS["⚖️  decision-synthesizer\nPicks winner, blends insights\n→ final recommendation"]
      KBW["💾 kb-writer-agent\nWrites P-xxx.md, D-xxx.md\nS-xxx/ + updates index.md"]
      NS["☁️  notion-sync-agent\n(manual, standalone)\nPushes P/D/S to Notion"]
      KB2[("📁 knowledge-base/\nP001…D001…S001…")]

      User --> Javit
      Javit --> PA
      PA --> KB
      KB --> LD
      LD --> A1 & A2
      A1 & A2 --> DS
      DS --> KBW
      KBW --> KB2
      KB2 -.->|"on demand"| NS
      NS -.-> Notion[("🗃️  Notion\n3 linked DBs")]
```