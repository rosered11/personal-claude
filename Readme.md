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


client_ref
round_ref 

คุยรายละเอียดเพิ่มเกียวกับ mapping box 

จำเปนไหม

- ในการใช้งานครื่อง put to light สูงสุดมีกี่ sku ต่อ store ต่อ วัน
- สอบถาม เพิ่มว่า outbox กับ alert อยากให้ทำที่ worker ติดไรไหม / 
- team wms อาจจะไม่ทำ alert เรา handle อะไรได้บ้าง /
- wms ยังจำเป็นต้องมี outbox อยู่ไหม /
- อยากปรับ UI worker ใหม่ ไม่ว่าเปน ด่าน 1 หรือ ขาขึ้น ขาลง ควรจะอ่านแล้วเข้าใจกว่านี้ แล้ว ผมรู้
- ถ้าเราส่งข้อมูลด้วย outbox pattern เรายังจำเป็นต้องมี version อยู่ไหม /
- routing key ใช้ทำอะไรบ้าง /
- message_id กับ event_id ยุบรวมเหลือตัวเดียวได้ไหม /
- ตอนนี้เรา split project put to light ออกมาจาก wes เป็น standalone แล้ว อาจจะไม่ต้อง implement project นี้ใน solution ของ wes แล้ว
    แต่คุณเก็บ knowledge ทั้งหมด กับ journey ที่เกิดขึ้นไว้เป็น memory ด้วยเมื่อไหร่เริ่มทำ wes คุณต้องมี best solution พร้อมสำหรับงานนี้
    แล้วก็ อาจจะต้องลองลด table ที่ไม่ได้ใช้ออก ที่ยังคงติดเรื่อง wes อยู่ /


- ปรับ spec ใหม่ ไม่ว่าเปน ด่าน 1 หรือ ขาขึ้น ขาลง ควรจะอ่านแล้วเข้าใจกว่านี้, qty ตระกูล, G66 อะไรพวกนี้ vendor อ่านไม่เข้าใจ, พวกขีดค่า หรือ update version ไม่ต้องมี เราจะแสดง version update อย่างเดียว แล้วก็ช่วยปรับ schema เป็น version 1.0 หน่อยเพราะเรายังไม่ได้ให้ vendor ดูอาจจะยังไม่ต้อง track /

- routing key จำเป็นไหม /
- adapter_key จำเป็นไหม แล้วจะขึ้นค่าใหม่อีกทีเมื่อไหร่ /
- ผมแค่อยากลบ ตะกูลออกจาก qty เพราะ key อื่นไม่เหนมี
- framework structure code golang + UI
