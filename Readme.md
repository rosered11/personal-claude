# Readme

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

> "Process inbox/my-problem.md"

## Architecture

Target Objective

- ต้องแปลงเอกสาร มาเป็นรูปตามนี้
    - Overview architect
    - Componenet specific

- tools
    - drawio
    - excel specific แต่ละ component
    - เหตุผลที่ใช้

## Pending

- claude --resume 3c75e795-e0a7-4a81-8906-bcbf6cd6a9af -> (RFID)
$env:CLAUDE_LABEL="personal-put-to-light"; claude --resume 3f70402c-3fe1-4be4-9f88-58efdeec07f2
- claude --resume bc5b58c0-8a42-4ea3-b796-f0c7727ef53a -> (MatDoc)
- claude --resume fd097914-d31e-4a33-838e-0e1d16ca1cc9 -> (Kafka Solution)
- claude --resume 8e1a37f2-91ec-4580-accb-99804be48e14 -> Estimate Cost ELK
$env:CLAUDE_LABEL="personal-estimate-oms-resource-for-infra"; claude --resume 7913fb21-6bfa-4c2f-b0dd-91d77d133506


## Note

ทำเอกสาร focus 2 system: CFW, CRL Muji
    - เน้นเรื่องการประเมิน resource ที่จะใช้
    - ต้อง list ให้รู้ว่าระบบต้องใช้อะไรบ้าง และใช้เพราะอะไร (end to end เช่น WMS,SPC,TMS)
    
