---
name: ptl-split-from-wes
description: PTL cross-dock แยกออกจาก WES เป็นโปรเจกต์ standalone แล้ว (2026-09-21) — ตอนกลับมาทำ WES อย่าลากของ PTL กลับเข้าไป
metadata: 
  node_type: memory
  type: project
  originSessionId: 3f70402c-3fe1-4be4-9f88-58efdeec07f2
  modified: 2026-09-21T08:30:58.182Z
---

**2026-09-21** ทีมตัดสินใจแยก **Put-to-Light (PTL) cross-dock ออกจาก WES เป็นโปรเจกต์ standalone** — ไม่ต้อง implement เส้นนี้ใน solution ของ WES อีกต่อไป

**สิ่งที่ทำไปแล้วตามการตัดสินใจนี้** (อยู่ที่ `inbox/push-to-light/`):
- `ptl-schema.sql` = ฐานของบริการ PTL เอง **7 ตาราง** (`adapter_inbound_event` · `work_dispatch` · `work_batch`/`work_carton` · `so_request` · `adapter_request_log` · `adapter_alert`)
- `wes-schema.sql` = ของ WES ล้วน **21 ตาราง** (outbound saga · putaway · multi-hop · conveyor/ASRS) — ตารางของ PTL ถูกตัดออกแล้ว เหลือ pointer ชี้ไป `ptl-schema.sql`
- ตัดขาดได้สะอาดเพราะ **ไม่มี FK ข้ามระหว่าง 2 กลุ่มเลยแม้แต่เส้นเดียว** และแอป PTL ไม่เคย query ตารางฝั่ง WES สักครั้ง
- `adapter_request_log` จงใจให้มีทั้ง 2 ฝั่ง — เป็นตาราง log ของ adapter pattern ที่แต่ละบริการควรมีของตัวเอง ไม่ใช่ของใช้ร่วม

**ตอนกลับมาเริ่มทำ WES**: ของที่ยังเป็นของ WES จริง ๆ คือ `wes-schema.sql` + `wes-database-schema.md` + `wes-integration-design.md` + `inbound-putaway-design.md` + `wes-multi-hop-orchestration-design.md` + `wes-stockpick-outbound-design.md`
**อย่า**ลากตารางของ PTL กลับเข้าไปใน WES อีก — เหตุผลบันทึกไว้ที่ G64 ใน [[ptl-design-knowledge-base]]
