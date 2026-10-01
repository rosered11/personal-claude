---
name: push-to-light-is-latest-version-only
description: inbox/push-to-light ไม่ commit ที่นี่ — ผู้ใช้เอาเนื้อหาไป commit ที่ project อื่น เก็บแค่ version ล่าสุด ⇒ ห้ามพึ่ง git เป็นตัวกู้
metadata:
  type: feedback
---

**ผู้ใช้บอกไว้ 2026-09-28**: *"อาจจะไม่มีการ commit ตอนนี้ เนื่องจากเนื้อหาทั้งหมดผมจะไปcommit ที่ project อีกที ... ยังไงผมก็เก็บแค่ version ล่าสุด"*

`D:\workspace\personal-claude\inbox\push-to-light\` **จะไม่ถูก commit ที่ repo นี้** (`git status` ขึ้น `??` ตลอดและนั่นถูกต้อง) · เนื้อหาไปอยู่ที่ project อื่น · ที่นี่เก็บแค่ฉบับล่าสุด

**Why**: ผมเสนอให้ commit ไป 2 ครั้งเพราะเห็นว่าไม่มีตัวกู้ · ผู้ใช้อธิบายว่าเป็นความตั้งใจ ⇒ **ไม่ต้องเสนออีก**

**How to apply**
- ❌ อย่าเสนอ `git add`/`commit` โฟลเดอร์นี้ · อย่าใช้ `git` เป็นแผนกู้
- 🔴 **แต่ยังต้องกันพลาดเอง** — เคยลบ `function ignore()` หายไปกับสคริปต์ลบบล็อก (G87 · 2026-09-28) แล้วกู้ไม่ได้ ต้องเขียนใหม่จากความเข้าใจ
- ⇒ **ก่อนลบบล็อกใหญ่หรือรันสคริปต์ที่ลบหลายบรรทัด ให้ `cp` ไฟล์ไว้ที่ scratchpad ก่อนทุกครั้ง** แล้วเทียบผลก่อนไปต่อ
- ⇒ สคริปต์ลบควร**พิมพ์ช่วงบรรทัดที่จะลบและจำนวน** ออกมาก่อน · ถ้าจำนวนเกินที่คาด ให้หยุดดูก่อน (102 บรรทัดที่หายไปมีสัญญาณนี้อยู่แล้วแต่ผมไม่ได้เอะใจ)
- ตัวยืนยันที่ใช้แทน git ได้: `asyncapi validate` · `redocly lint` · apply schema เข้า postgres จริง · render mermaid ทุก diagram · `tools/struct-vs-schema.py`

ดู [[prove-with-a-running-system]] · [[memory-lives-in-the-repo]] (ของ `.claude/memory/` คนละเรื่อง — อันนั้น track ใน git จริง)
