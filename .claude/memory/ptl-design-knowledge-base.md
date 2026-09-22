---
name: ptl-design-knowledge-base
description: แผนที่เอกสาร PTL cross-dock — gap log G1–G68 คือหัวใจ อ่านที่นั่นก่อนตัดสินใจอะไรใหม่
metadata: 
  node_type: memory
  type: reference
  originSessionId: 3f70402c-3fe1-4be4-9f88-58efdeec07f2
  modified: 2026-09-21T08:31:25.921Z
---

ความรู้ทั้งหมดของงาน PTL cross-dock อยู่ใน `inbox/push-to-light/` **อย่าออกแบบใหม่ก่อนอ่าน** เพราะหลายข้อเคยตัดสินไปแล้วพร้อมเหตุผลและหลักฐานการทดสอบ

| ไฟล์ | คืออะไร |
|---|---|
| `wes-batch-pull-design.md` | **หัวใจ** — design + **gap log G1–G68** ทุกข้อมี: ปัญหา / ผลกระทบ / สิ่งที่ตัดสิน / **ผลทดสอบจริง** · ข้อที่กลับคำก็บันทึกไว้ว่ากลับเพราะอะไร |
| `ptl-batch-api-spec.md` (+ `.apib` / `.html`) | สัญญาที่ส่งให้ vendor — **เวอร์ชัน 1.0** · 6 ชนิดที่ vendor ส่ง · 4 ชนิดที่ vendor รับ · 4 control (freeze) · 13 ignore reason · ดู [[vendor-docs-style]] ก่อนแก้ |
| `wes-internal-api-spec.md` | เส้น HTTP ภายใน 4 เส้นระหว่าง WMS ↔ worker/Proxy |
| `ptl-rabbitmq-topology.md` | exchange/queue ทั้งหมด — **§10 เป็นส่วนเดียวที่ส่งให้ vendor ได้** |
| `wms-batch-schema.sql` / `.md` | ฝั่ง WMS (คนละทีม คนละฐาน) |
| `ptl-schema.sql` + `ptl-database-schema.md` | ฝั่งเรา 8 ตาราง (ดู [[ptl-split-from-wes]]) |
| `app/` | **ระบบจำลองที่รันได้จริง** `docker compose up` — 5 คอนเทนเนอร์ เดินครบวงจริง ใช้พิสูจน์ทุกข้อที่ตัดสิน |

**เสาหลักของ design ที่ไม่ควรรื้อโดยไม่อ่าน gap ก่อน**
- **outbox ที่ WMS ห้ามหาย** (G53/G59) — แถว outbox ต้องเกิดในทรานแซกชันเดียวกับการกดปุ่ม ไม่งั้นได้ dual-write กลับมา
- **ขาลงเป็น push** WMS → worker (G59 กลับคำจาก G58 ที่เคยเป็น pull/lease)
- **ขาขึ้น 1 คิวต่อไซต์ + `x-single-active-consumer`** (G56 เลิกใช้ x-consistent-hash)
- **`seq` มีเฉพาะเลนของกล่อง** (G60 · แคบลงที่ G66) — `pocket_map`/`batch_progress`/`work_state` ไม่มี · ของเก่ามาช้ากว่าที่ส่งต่อไปแล้ว = ข้าม + alert (G65)
- **`version` ต่อ assignment** (G62) — ลำดับที่ข้อความมาถึงไม่ใช่หลักฐานว่าอันไหนใหม่กว่า · **ขยับได้เฉพาะตอนมีข้อความออกไปหา vendor**
- **`message_id` ชื่อเดียวทั้ง 2 ทิศ** + `ref_message_id` สำหรับอ้างข้ามทิศ (G63) · **unique คือ `(site, message_id)`** (G68)
- **`pocket_map` ต้องมาจากผนังที่ถืองานอยู่ตอนนี้** (G66) — ผังคือ "แผน" ไม่ใช่เหตุการณ์ที่เกิดแล้ว
- **alert เรื่องงานอยู่ที่ worker ด้วย** (G61/G62) เพราะทีม WMS อาจไม่ทำฝั่งเขา
- **รับ vendor ได้หลายเจ้า — รอยต่อคือ `site`** (G68) ไม่ใช่ `adapter_key` (ตัดทิ้งแล้ว) · ทะเบียนอยู่ที่ `spc.site_vendor`
- **routing key ขาที่ vendor ส่งไม่ได้ routing แล้ว** (fanout) แต่ยังบังคับใส่และ**เราตรวจ** (G67) — คำอธิบายเต็มอยู่ `ptl-rabbitmq-topology.md` หัวข้อ 5
