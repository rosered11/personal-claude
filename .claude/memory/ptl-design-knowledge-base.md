---
name: ptl-design-knowledge-base
description: แผนที่เอกสาร PTL cross-dock — gap log G1–G88 คือหัวใจ อ่านที่นั่นก่อนตัดสินใจอะไรใหม่
metadata: 
  node_type: memory
  type: reference
  originSessionId: 3f70402c-3fe1-4be4-9f88-58efdeec07f2
  modified: 2026-09-21T08:31:25.921Z
---

**เอกสาร**ทั้งหมดของงาน PTL cross-dock อยู่ใน `inbox/push-to-light/` ส่วน **โค้ด** ย้ายไป repo `D:\workspace\ptl_worker` แล้ว (2026-09-23) **อย่าออกแบบใหม่ก่อนอ่าน** เพราะหลายข้อเคยตัดสินไปแล้วพร้อมเหตุผลและหลักฐานการทดสอบ

| ไฟล์ | คืออะไร |
|---|---|
| `ptl_worker/src/ptl-handover.md` 🔴 **อยู่คนละ repo** | **เริ่มที่นี่** `[ส่งมอบให้ทีม ptl_worker 2026-09-24 — ฝั่งเอกสารไม่มีสำเนาแล้ว]` — แผนที่ทั้งระบบใน 10 หัวข้อ (ระบบทำอะไร · 3 ฝ่าย · สถาปัตยกรรม · 8 กฎห้ามรื้อ · operations · แผนที่ไฟล์) |
| `ptl-decision-log.md` | **หัวใจ — "ทำไม"** · **gap log G1–G88** ทุกข้อมี: ปัญหา / ผลกระทบ / สิ่งที่ตัดสิน / **ผลทดสอบจริง** · หัวข้อ 0 รวม **ข้อที่กลับคำ** ไว้ที่เดียว<br>`[2026-09-23]` แยกออกมาจาก `wes-batch-pull-design.md` แล้ว**ลบไฟล์เดิมทิ้ง** — ชื่อเดิมผิดทั้ง 2 คำ ("wes" แยกไปตั้งแต่ G64 · "pull" กลับเป็น push ตั้งแต่ G59) และครึ่งไฟล์เป็นสำเนาสัญญาที่ค้างเวอร์ชันเก่า |
| `ptl-batch-api-spec.md` (+ `.asyncapi.yaml` / `.html`) | สัญญาที่ส่งให้ vendor — **เวอร์ชัน 1.0** · 6 ชนิดที่ vendor ส่ง · **3 ชนิดที่ vendor รับ** · 4 control (freeze) · 13 ignore reason · ดู [[vendor-docs-style]] ก่อนแก้ |
| `wms-internal-api-spec.md` | เส้น HTTP ภายใน 4 เส้นระหว่าง WMS ↔ worker/Proxy |
| `wms-internal-api.openapi.yaml` (+ `.openapi.html`) | เส้นภายในฉบับ **เครื่องอ่าน** — OpenAPI 3.1 · `[ใหม่ 2026-09-24]` · HTTP ใช้ OpenAPI ส่วนฝั่ง vendor เป็นคิวจึงใช้ AsyncAPI |
| `ptl-rabbitmq-topology.md` | exchange/queue ทั้งหมด — **§10 เป็นส่วนเดียวที่ส่งให้ vendor ได้** |
| `wms-batch-schema.sql` / `.md` | ฝั่ง WMS (คนละทีม คนละฐาน) |
| `ptl-schema.sql` + `ptl-database-schema.md` | ฝั่งเรา 8 ตาราง (ดู [[ptl-split-from-wes]]) |
| **โค้ด** | 🔴 **ไม่อยู่ในโฟลเดอร์นี้แล้ว `[2026-09-23]`** — ย้ายไป repo **`D:\workspace\ptl_worker`** (ระบบจำลองอยู่ที่ `src/`) ให้อีกทีมดูแล · `docker compose up` ได้ 5 คอนเทนเนอร์ เดินครบวงจริง ใช้พิสูจน์ทุกข้อที่ตัดสิน · เอกสารที่นี่อ้างถึงด้วย path `ptl_worker/src/...` |

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
- **ไม่มี `work_resync` แล้ว** (G69 แทนที่ G45) — กู้ด้วย **จอ "ส่งซ้ำ" ของ worker** (`/replay`) ที่ publish payload เดิมด้วย `message_id` เดิม
  ⇒ vendor ไม่ต้องทำ snapshot semantics · 🔴 **เหตุที่ต้องมีเครื่องมือนี้: คิว `ptl.control` มี TTL 24 ชม.** ถ้า vendor ล่มนานกว่านั้น
  คำสั่งยกเลิก/ปิด batch จะหมดอายุหายจาก broker ถาวร (ฝั่งเรา publish สำเร็จไปแล้วจึงไม่มี retry) ⇒ **ผนังจุดไฟให้งานที่ตายแล้ว**
  · `spc.work_dispatch` จึงต้องเก็บ **90 วัน** — ถ้ามีคน purge เครื่องมือกู้นี้หายไปด้วย
