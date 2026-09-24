---
name: ptl-app-changes-go-to-ptl-worker-repo
description: จะแก้ app/โค้ดของ put to light ต้องไปทำที่ repo D:\workspace\ptl_worker ไม่ใช่ที่โฟลเดอร์เอกสาร
metadata:
  type: feedback
---

**ผู้ใช้สั่งไว้ 2026-09-23**: ถ้าจะแก้ **app ของ put to light** ให้สั่งงานตามนี้ ไม่ใช่ลงมือแก้เองจากโฟลเดอร์เอกสาร

1. เปิด Claude Code โดยตั้ง **working directory = `D:\workspace\ptl_worker`**
2. **สั่งงานเป็นภาษาธรรมชาติได้เลย** — บอกแค่ว่าอยากได้อะไร แล้วปล่อยให้มันเลือก agent เองตาม **ตาราง routing ใน `CLAUDE.md` ของ repo นั้น**

**Why**: โค้ดถูก move ไปให้อีกทีมดูแลตั้งแต่ 2026-09-23 · repo นั้นมี CLAUDE.md + `.claude/agents/` (8 ตัว: `architect` · `api-web-engineer` · `worker-engineer` · `database-engineer` · `test-engineer` · `code-reviewer` · `devops-engineer` · `project-oracle`) + `.claude/rules/` + memory ของตัวเองครบ — การแก้จากข้างนอกคือการข้ามทีมและข้ามกติกาของ repo นั้นทั้งชุด

**How to apply**
- ที่ `personal-claude` เราดูแล **สัญญา + schema + เอกสาร** เท่านั้น (ดู [[ptl-teams-and-boundaries]])
- ถ้าเรื่องที่คุยอยู่จบลงที่ "ต้องแก้โค้ด" → **หยุดที่ข้อเสนอ** แล้วบอกผู้ใช้ให้ไปสั่งที่ repo `ptl_worker` ตาม 2 ข้อข้างบน ไม่ต้องไปแก้ไฟล์ข้ามให้
- ของที่ยัง**ต้องตรงกันข้าม repo**: `ptl-schema.sql` ↔ `src/migrations/spc/00001_baseline_spc.sql` และ `wms-batch-schema.sql` ↔ `20-wms.sql` (ต่างกันแค่ header) — แก้ฝั่งเอกสารแล้วต้องบอกให้ repo นั้นแก้ตาม
- `inbox/push-to-light/_archive_app/` **ผู้ใช้สั่งให้เก็บไว้ก่อน** (2026-09-23) แม้จะเป็นสำเนาเก่ากว่า repo `ptl_worker` — อย่าเผลอลบตามกฎ [[delete-dead-things-not-archive]]

ดู [[ptl-design-knowledge-base]]
