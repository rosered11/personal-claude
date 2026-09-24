---
name: ptl-teams-and-boundaries
description: "ใครทำอะไรในงาน PTL — WMS คนละทีมคนละฐาน, vendor ยังไม่เริ่มเขียนโค้ด (ณ 2026-09-21), ผู้ใช้เป็นคนคุมฝั่ง integration"
metadata: 
  node_type: memory
  type: project
  originSessionId: 3f70402c-3fe1-4be4-9f88-58efdeec07f2
  modified: 2026-09-21T08:33:09.112Z
---

**เส้นแบ่งความรับผิดชอบของงาน PTL cross-dock** (สถานะ ณ 2026-09-21)

| ฝ่าย | ใคร | สถานะ |
|---|---|---|
| **WMS** | **อีกทีมหนึ่ง คนละฐานข้อมูล** | คุยกันผ่าน HTTP 4 เส้นเท่านั้น — ห้ามอ่าน/เขียนตารางข้ามทีม |
| **Proxy + PTL adapter worker** | **ฝั่งผู้ใช้** (คนที่คุยกับเราอยู่) เป็นเจ้าของ **สัญญา + schema + เอกสาร** · 🔴 **โค้ดย้ายไปให้อีกทีมดูแลแล้ว 2026-09-23** ที่ repo `D:\workspace\ptl_worker` (`src/` = worker + ระบบจำลอง 5 คอนเทนเนอร์) | เป็นเจ้าของ `ptl-schema.sql` · RabbitMQ · การส่งต่อและ alert · เวลาแก้สัญญาต้องแจ้ง repo `ptl_worker` ด้วย เพราะ `ptl-schema.sql` ต้องตรงกับ `ptl_worker/src/migrations/spc/00001_baseline_spc.sql` เสมอ |
| **vendor (เจ้าของเครื่อง PTL)** | บริษัทภายนอก | 🔴 **ยังไม่ได้ลงมือเขียนโค้ดอะไรเลย** ⇒ ยังแก้สัญญาได้อิสระ (ยืนยัน 2026-09-21) |

**ผลของการที่ vendor ยังไม่เริ่ม**: เปลี่ยนสัญญาได้โดยไม่ต้องทำ backward compatibility — ใช้สิทธิ์นี้ไปแล้วกับการยุบ `event_id`→`message_id` (G63) · ถ้าวันหนึ่ง vendor เริ่มแล้ว ต้นทุนการเปลี่ยนจะพลิกทันที ให้ถามสถานะก่อนเสนอเปลี่ยนชื่อ field

**ผลของการที่ WMS เป็นคนละทีม**:
- worker **ไม่ query `wms.*` เลยสักบรรทัด**
- ถ้าทีม WMS ไม่ทำบางอย่าง (เช่น จอ alert) เราต้องรับมาทำที่ฝั่งเราแทน — ทำไปแล้วใน G61/G62
- **สิ่งเดียวที่ขอจากทีม WMS แล้วขาดไม่ได้**: คง `reason` ไว้ใน response ของ `POST /internal/batch-events` (ชั้น A ของ alert ทั้งหมดพึ่งค่านี้)
- **ยังมี 3 เรื่องที่เรามองไม่เห็นและต้องตกลงว่าใครรับ**: `batch_overdue_unassigned` · `return_lines_pending` · `leftover_on_hold`
- 🔴 **ค้างอยู่: ต้องแจ้งทีม WMS เรื่องสัญญาที่เปลี่ยนไป 3 ข้อ** (ยังไม่ได้แจ้ง)
  1. `POST /internal/batch-events` — field `event_id` → **`message_id`** (G63)
  2. `POST /internal/work-messages` — field `routing_key` → **`lane`** (G67) · และคอลัมน์ `dispatch_work_outbox.routing_key` ก็เปลี่ยนชื่อตาม
  3. **ตัด `adapter_key` ออกจาก payload** แล้ว (G68) · `schema_version` เป็น **`1.0`** ไม่ใช่ `4.1`
  4. **ตัดชนิดข้อความ `work_resync` ทิ้ง** (G69) ⇒ `dispatch_work_outbox` ไม่มีคอลัมน์ `resync_id` / `is_last` แล้ว และ CHECK ของ `type` เหลือ 3 ชนิด — กู้ด้วยจอ "ส่งซ้ำ" ของ worker แทน ซึ่ง**ไม่ต้องให้ WMS ทำอะไรเลย**

🔴 **ทีม `ptl_worker` แจ้งรายการค้าง 14 ข้อ (2026-09-23)** — ตรวจแล้ว **จริงทุกข้อ** · ในนั้นมี **บั๊กที่ทำให้ข้อมูลผิดแบบเงียบ 2 ข้อ**: ไม่มี AMQP reconnect · publisher confirm ใช้ channel รวมจึงจับคู่ใบยืนยันผิดได้ (`internal/mq/mq.go`) · และ `/internal/work-messages` insert ก่อน validate ทำให้ตอบ `400` ทั้งที่รับบางใบไปแล้ว
คำตอบเต็ม + ของที่เราเจอเพิ่ม (ไม่มี job ลบตาม retention 90 วัน) อยู่ที่ `inbox/push-to-light/ptl-notice-to-worker-team.md`

**vendor หลายเจ้าพร้อมกัน — รับได้อยู่แล้ว** (G68) · รอยต่อคือ **`site`**: 1 ไซต์ = 1 vendor = exchange/คิว/credential/consumer/`station_code` ชุดของตัวเอง
⇒ ปัญหาของเจ้าหนึ่งไม่ลามข้ามไปอีกเจ้า · ทะเบียนว่าไซต์ไหนของใครและอยู่สัญญาเวอร์ชันไหนอยู่ที่ **`spc.site_vendor`** (จอ alert แสดงคอลัมน์ "ดูแลโดย")
· ที่ยังไม่ได้ทำเพราะรอให้มีเจ้าที่ 2 จริงก่อน: **SLA ต่อไซต์** (`PTL_SLA_*` ตอนนี้เป็นค่าเดียวทั้งระบบ)

ดู [[ptl-design-knowledge-base]] · [[ptl-split-from-wes]] · [[vendor-docs-style]]
