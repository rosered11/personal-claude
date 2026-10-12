---
name: handoff-goes-in-the-file-not-the-chat
description: ข้อความ cross-session หายได้เงียบ ๆ — ของที่ส่งให้ทีมอื่นต้องเขียนลงไฟล์ notice ก่อนเสมอ
metadata:
  node_type: memory
  type: feedback
  originSessionId: 3f70402c-3fe1-4be4-9f88-58efdeec07f2
  modified: 2026-10-12T06:25:07.079Z
---

**ส่งอะไรให้ทีมอื่น ต้องเขียนลงไฟล์ notice ก่อน แล้วค่อยส่งข้อความชี้ไปหาไฟล์** — ห้ามให้ข้อความแชทเป็นที่เดียวที่เนื้อหานั้นอยู่

**Why**: 2026-10-12 ส่งข้อความหา session `ptl-worker-d8` ไป **4 ฉบับ** (G134 · G135 · G136 · G137 · G138) · ผู้ใช้ปิด session นั้น ⇒ **ไม่มีใครได้อ่านเลยแม้แต่ฉบับเดียว** · ผลการส่งทุกครั้งเขียนบอกไว้ชัดว่า *"in that session's inbox, **not yet read** by its Claude"* — ผมเห็นแต่ไม่ได้ตีความว่า "ยังไม่ถึงมือใคร"
🔑 **G135 กับ G138 รอดเพราะเผอิญเขียน section ลง `ptl-notice-to-worker-team.md` ด้วย · G134 · G136 · G137 ไม่ได้เขียน ⇒ หายทั้งหมด** และเนื้อหา G137 คือ DDL drift 8 จุดที่ใช้เวลาหานาน

**How to apply**:
- **ลำดับที่ถูก**: เขียน section ลง `ptl-notice-to-worker-team.md` / `ptl-notice-to-wms-team.md` → **แล้วค่อย** `SendMessage` ที่สรุปสั้นแล้วชี้ว่า "อ่านเต็มที่ไฟล์ไหน หัวข้อไหน"
- ไฟล์ notice เรียง **gap ใหม่อยู่บนสุด** · section เก่าไม่แก้ (เป็นบันทึกว่าบอกอะไรไปเมื่อไร) ของที่เปลี่ยนทีหลังให้ section ใหม่ทับความหมาย
- 🔴 **"ส่งสำเร็จ" ≠ "ถึงมือ"** — `SendMessage` คืน success แค่ว่าเข้า inbox · ถ้าเรื่องสำคัญให้**ขอยืนยันกลับ** และอย่าถือว่าส่งถึงจนได้คำตอบ
- ชื่อ session ฝั่ง `ptl-worker` **เปลี่ยนแทบทุกครั้ง** (`d8` → `d5` → `26`) ⇒ `ListAgents` ก่อนส่งทุกครั้ง · ส่งไม่ผ่านแปลว่าคนละ session ไม่ใช่ชื่อพิมพ์ผิด
- ใช้กับทุกทีม รวม WMS ที่ยังไม่มีใครส่ง notice ให้เลย

ดู [[ptl-teams-and-boundaries]] · [[delete-dead-things-not-archive]]
