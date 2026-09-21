---
name: ptl-design-knowledge-base
description: แผนที่เอกสาร PTL cross-dock — gap log G1–G64 คือหัวใจ อ่านที่นั่นก่อนตัดสินใจอะไรใหม่
metadata: 
  node_type: memory
  type: reference
  originSessionId: 3f70402c-3fe1-4be4-9f88-58efdeec07f2
  modified: 2026-09-21T08:31:25.921Z
---

ความรู้ทั้งหมดของงาน PTL cross-dock อยู่ใน `inbox/push-to-light/` **อย่าออกแบบใหม่ก่อนอ่าน** เพราะหลายข้อเคยตัดสินไปแล้วพร้อมเหตุผลและหลักฐานการทดสอบ

| ไฟล์ | คืออะไร |
|---|---|
| `wes-batch-pull-design.md` | **หัวใจ** — design + **gap log G1–G64** ทุกข้อมี: ปัญหา / ผลกระทบ / สิ่งที่ตัดสิน / **ผลทดสอบจริง** · ข้อที่กลับคำก็บันทึกไว้ว่ากลับเพราะอะไร |
| `ptl-batch-api-spec.md` (+ `.apib` / `.html`) | สัญญาที่ส่งให้ vendor — v4.1 · 6 event ขาขึ้น · 4 ชนิดขาลง · 4 control (freeze) · 13 ignore reason |
| `wes-internal-api-spec.md` | เส้น HTTP ภายใน 4 เส้นระหว่าง WMS ↔ worker/Proxy |
| `ptl-rabbitmq-topology.md` | exchange/queue ทั้งหมด — **§10 เป็นส่วนเดียวที่ส่งให้ vendor ได้** |
| `wms-batch-schema.sql` / `.md` | ฝั่ง WMS (คนละทีม คนละฐาน) |
| `ptl-schema.sql` | ฝั่งเรา 7 ตาราง (ดู [[ptl-split-from-wes]]) |
| `app/` | **ระบบจำลองที่รันได้จริง** `docker compose up` — 5 คอนเทนเนอร์ เดินครบวงจริง ใช้พิสูจน์ทุกข้อที่ตัดสิน |

**เสาหลักของ design ที่ไม่ควรรื้อโดยไม่อ่าน gap ก่อน**
- **outbox ที่ WMS ห้ามหาย** (G53/G59) — แถว outbox ต้องเกิดในทรานแซกชันเดียวกับการกดปุ่ม ไม่งั้นได้ dual-write กลับมา
- **ขาลงเป็น push** WMS → worker (G59 กลับคำจาก G58 ที่เคยเป็น pull/lease)
- **ขาขึ้น 1 คิวต่อไซต์ + `x-single-active-consumer`** (G56 เลิกใช้ x-consistent-hash)
- **`seq` ต่อเลน** (G60) และ **`version` ต่อ assignment** (G62/G63) — ลำดับที่ข้อความมาถึงไม่ใช่หลักฐานว่าอันไหนใหม่กว่า
- **`message_id` ชื่อเดียวทั้ง 2 ทิศ** + `ref_message_id` สำหรับอ้างข้ามทิศ (G63)
- **alert เรื่องงานอยู่ที่ worker ด้วย** (G61/G62) เพราะทีม WMS อาจไม่ทำฝั่งเขา
