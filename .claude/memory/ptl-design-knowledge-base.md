---
name: ptl-design-knowledge-base
description: แผนที่เอกสาร PTL cross-dock — gap log ใน ptl-decision-log.md คือหัวใจ อ่านที่นั่นก่อนตัดสินใจอะไรใหม่
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
| `ptl-decision-log.md` | **หัวใจ — "ทำไม"** · **gap log (ต่อเลขเรื่อย ๆ — เปิดดูท้ายตารางเอา ไม่ต้องเชื่อเลขที่จดไว้ที่นี่)** ทุกข้อมี: ปัญหา / ผลกระทบ / สิ่งที่ตัดสิน / **ผลทดสอบจริง** · หัวข้อ 0 รวม **ข้อที่กลับคำ** ไว้ที่เดียว<br>`[2026-09-23]` แยกออกมาจาก `wes-batch-pull-design.md` แล้ว**ลบไฟล์เดิมทิ้ง** — ชื่อเดิมผิดทั้ง 2 คำ ("wes" แยกไปตั้งแต่ G64 · "pull" กลับเป็น push ตั้งแต่ G59) และครึ่งไฟล์เป็นสำเนาสัญญาที่ค้างเวอร์ชันเก่า |
| `ptl-batch-api-spec.md` (+ `.asyncapi.yaml`) | สัญญาที่ส่งให้ vendor — **เวอร์ชัน 1.0** · 6 ชนิดที่ vendor ส่ง · **3 ชนิดที่ vendor รับ** · **2 control** `[G87 — เดิม 4 · ตัด `batch_cancel_requested` + `event_ignored`]` · 13 ignore reason · ดู [[vendor-docs-style]] และ [[asyncapi-yaml-is-hand-maintained]] ก่อนแก้ |
| `wms-internal-api.openapi.yaml` 🔴 **ฉบับเดียวที่ส่งให้ทีม WMS** | เส้น HTTP ภายใน **6 เส้น** (`batch-events` · `work-messages` · `stations` · `so` + `health/live` · `health/ready`) `[ผู้ใช้สั่ง 2026-09-29: "ส่งแค่ไฟล์นี้ไฟล์เดียว" ⇒ ทุกกติกาต้องอยู่ในไฟล์นี้ **ห้ามอ้างอิงไป .md**]`<br>· schema ของ payload **11 ชุด** (`WmsOutbound_*` 5 + `VendorEvent_*` 6) เป็น **ของ generate จาก `ptl-batch-api.asyncapi.yaml`** ⇒ แก้ด้วยมือไม่ได้ · ต้องรัน `python tools/sync-vendor-payloads.py` ทุกครั้งที่สัญญา vendor เปลี่ยน (มี `--check` ให้กันก่อนส่ง) |
| `wms-internal-api-spec.md` | ฉบับ **คนอ่าน ใช้ภายในเท่านั้น ไม่ได้ส่งใคร** — ถ้าขัดกับ openapi ให้ถือ openapi เป็นจริง |
| `ptl-rabbitmq-topology.md` | exchange/queue ทั้งหมด — **§10 เป็นส่วนเดียวที่ส่งให้ vendor ได้** |
| `wms-batch-schema.sql` / `.md` | ฝั่ง WMS (คนละทีม คนละฐาน) |
| `ptl-schema.sql` + `ptl-database-schema.md` | ฝั่งเรา **9 ตาราง** (ดู [[ptl-split-from-wes]]) |
| **โค้ด** | 🔴 **ไม่อยู่ในโฟลเดอร์นี้แล้ว `[2026-09-23]`** — ย้ายไป repo **`D:\workspace\ptl_worker`** (ระบบจำลองอยู่ที่ `src/`) ให้อีกทีมดูแล · `docker compose up` ได้ 5 คอนเทนเนอร์ เดินครบวงจริง ใช้พิสูจน์ทุกข้อที่ตัดสิน · เอกสารที่นี่อ้างถึงด้วย path `ptl_worker/src/...` |

**เสาหลักของ design ที่ไม่ควรรื้อโดยไม่อ่าน gap ก่อน**
- **outbox ที่ WMS ห้ามหาย** (G53/G59) — แถว outbox ต้องเกิดในทรานแซกชันเดียวกับการกดปุ่ม ไม่งั้นได้ dual-write กลับมา
- **ขาลงเป็น push** WMS → worker (G59 กลับคำจาก G58 ที่เคยเป็น pull/lease)
- **ขาขึ้น 1 คิวต่อไซต์ + `x-single-active-consumer`** (G56 เลิกใช้ x-consistent-hash)
- **`seq` มีเฉพาะเลนของกล่อง** (G60 · แคบลงที่ G66) — `pocket_map`/`batch_completed`/`work_state` ไม่มี · ของเก่ามาช้ากว่าที่ส่งต่อไปแล้ว = ข้าม + alert (G65)<br>⚠️ **ไม่มี `batch_progress` แล้ว** `[G87]` — เหลือ `batch_completed` ที่มีความหมายเดียว ไม่มี field `state`
- **`version` ต่อ assignment — ตัวเดียวในสัญญา และ `worker` เป็นเจ้าของ** `[G92 · 2026-09-29 กลับคำจาก G62/G79]` — worker mint ตอน `INSERT spc.work_dispatch` · **`batches[].version` ถูกตัดออกจากสัญญา vendor แล้ว** แทนด้วยตัวนับของจริง (`line_count` · `qty_total` · `carton_count`) ที่ vendor ต้องนับจากของที่ตัวเองถือ · WMS **ไม่ต้องทำ version ให้เราเลย**
- **`message_id` ชื่อเดียวทั้ง 2 ทิศ** + `ref_message_id` สำหรับอ้างข้ามทิศ (G63) · **unique คือ `(site, message_id)`** (G68)
- **`pocket_map` ต้องมาจากผนังที่ถืองานอยู่ตอนนี้** (G66) — ผังคือ "แผน" ไม่ใช่เหตุการณ์ที่เกิดแล้ว
- **alert เรื่องงานอยู่ที่ worker ด้วย** (G61/G62) เพราะทีม WMS อาจไม่ทำฝั่งเขา
- **รับ vendor ได้หลายเจ้า — รอยต่อคือ `site`** (G68) ไม่ใช่ `adapter_key` (ตัดทิ้งแล้ว) · ทะเบียนอยู่ที่ `spc.site_vendor`
- 🔴 **เลิกบังคับ routing key ฝั่ง vendor ทั้งหมด** `[G83 · 2026-09-25 กลับคำจาก G67]` — fanout ไม่ route ด้วยมัน · worker คำนวณเลนจาก body · เหตุผลของผู้ใช้: *"ถ้าไม่ใช้ควรเอาออกเสมอ เพื่อลด process"* และมันเป็น**ค่าที่ derive ซ้ำ** (ดู [[cut-what-is-not-used]]) ⇒ `lane_key_mismatch` ถูกตัดออกจาก alert ด้วย · **`lane` ในตาราง outbox ≠ routing key** และค่าที่ใช้ได้เหลือ `assignment:<ref>` (ขาคิวงานทุกชนิด) กับ `station:<code>` (ขา control) — ⛔ **`batch:<no>` ใช้ไม่ได้แล้ว** `[G82]`
- **ไม่มี `work_resync` แล้ว** (G69 แทนที่ G45) — กู้ด้วย **จอ "ส่งซ้ำ" ของ worker** (`/replay`) ที่ publish payload เดิมด้วย `message_id` เดิม
  ⇒ vendor ไม่ต้องทำ snapshot semantics · 🔴 **เหตุที่ต้องมีเครื่องมือนี้: คิว `ptl.control` มี TTL 24 ชม.** ถ้า vendor ล่มนานกว่านั้น
  คำสั่งยกเลิก/ปิด batch จะหมดอายุหายจาก broker ถาวร (ฝั่งเรา publish สำเร็จไปแล้วจึงไม่มี retry) ⇒ **ผนังจุดไฟให้งานที่ตายแล้ว**
  · `spc.work_dispatch` จึงต้องเก็บ **90 วัน** — ถ้ามีคน purge เครื่องมือกู้นี้หายไปด้วย
