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
| **Proxy + PTL adapter worker** | **ฝั่งผู้ใช้** (คนที่คุยกับเราอยู่) | เป็นเจ้าของ `ptl-schema.sql` · RabbitMQ · การส่งต่อและ alert |
| **vendor (เจ้าของเครื่อง PTL)** | บริษัทภายนอก | 🔴 **ยังไม่ได้ลงมือเขียนโค้ดอะไรเลย** ⇒ ยังแก้สัญญาได้อิสระ (ยืนยัน 2026-09-21) |

**ผลของการที่ vendor ยังไม่เริ่ม**: เปลี่ยนสัญญาได้โดยไม่ต้องทำ backward compatibility — ใช้สิทธิ์นี้ไปแล้วกับการยุบ `event_id`→`message_id` (G63) · ถ้าวันหนึ่ง vendor เริ่มแล้ว ต้นทุนการเปลี่ยนจะพลิกทันที ให้ถามสถานะก่อนเสนอเปลี่ยนชื่อ field

**ผลของการที่ WMS เป็นคนละทีม**:
- worker **ไม่ query `wms.*` เลยสักบรรทัด**
- ถ้าทีม WMS ไม่ทำบางอย่าง (เช่น จอ alert) เราต้องรับมาทำที่ฝั่งเราแทน — ทำไปแล้วใน G61/G62
- **สิ่งเดียวที่ขอจากทีม WMS แล้วขาดไม่ได้**: คง `reason` ไว้ใน response ของ `POST /internal/batch-events` (ชั้น A ของ alert ทั้งหมดพึ่งค่านี้)
- **ยังมี 3 เรื่องที่เรามองไม่เห็นและต้องตกลงว่าใครรับ**: `batch_overdue_unassigned` · `return_lines_pending` · `leftover_on_hold`
- 🔴 **ต้องแจ้งทีม WMS** ว่า field ใน `POST /internal/batch-events` เปลี่ยนชื่อ `event_id` → `message_id` แล้ว (G63)

ดู [[ptl-design-knowledge-base]] · [[ptl-split-from-wes]]
