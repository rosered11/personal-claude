---
name: asyncapi-yaml-is-hand-maintained
description: ptl-batch-api.asyncapi.yaml ผู้ใช้แก้ wording เองแล้ว — ห้าม generate ทับ ห้ามแก้ description ถ้าไม่จำเป็น
metadata:
  type: feedback
---

**ผู้ใช้สั่งไว้ 2026-09-25**: *"ผมมีปรับ wording ใน asyncapi ช่วยอย่าแก้ description ที่ผมปรับใหม่ถ้าไม่จำเป็น"*

`inbox/push-to-light/ptl-batch-api.asyncapi.yaml` **เป็นไฟล์ที่คนดูแลด้วยมือแล้ว ไม่ใช่ไฟล์ generate**

**Why**: ตอนสร้างครั้งแรก (2026-09-24) ผมประกอบมันขึ้นมาจากสคริปต์ใน scratchpad (`gen_asyncapi.py` → `gen2.py` → `gen3.py`) · ถ้าใครรันสคริปต์พวกนั้นซ้ำ **ข้อความที่ผู้ใช้ปรับจะถูกเขียนทับหายหมด** และจะไม่มีใครรู้ตัวเพราะ validate ก็ยังผ่าน

**How to apply**
- 🔴 **ห้าม regenerate ไฟล์ `.yaml` จากสคริปต์อีก** — สคริปต์ใน scratchpad ถือว่าปลดระวางแล้ว
- แก้เนื้อหาให้แก้ **ในไฟล์ yaml โดยตรง** ด้วย edit ที่เจาะจงจุด (ไม่ใช่เขียนทั้งไฟล์ใหม่)
- **แตะเฉพาะที่จำเป็น** — ถ้าต้องแก้ field/schema ก็แก้เฉพาะจุดนั้น **อย่าไปเกลา description ที่ผู้ใช้เขียนเอง**
- ผู้ใช้เปลี่ยน `info.title` เป็น **"Vendor Spec"** และเขียน description ใหม่เอง (เช่น "เดินด้วย RabbitMQ อย่างเดียว") — ของเดิมที่ผมเขียนไม่ต้องเอากลับมา
- **`.asyncapi.html` เป็นไฟล์ generate จาก yaml** ⇒ แก้ yaml แล้ว **ต้อง regenerate html ทุกครั้ง** (คำสั่งอยู่ `README.md` หัวข้อ 6) · การ regenerate html ไม่ทำลาย wording เพราะมันแค่เรนเดอร์
- ตรวจหลังแก้เสมอ: `asyncapi validate` ต้องได้ *"is valid ... don't have governance issues"*

เทียบกับอีกไฟล์: `wms-internal-api.openapi.yaml` **ยังเป็นไฟล์ generate อยู่** (จาก `gen_openapi.py`) ถ้าวันหนึ่งผู้ใช้แก้มือเหมือนกัน ให้ปลดระวาง generator ตัวนั้นด้วย

ดู [[vendor-docs-style]] · [[ptl-design-knowledge-base]]
