---
name: ai-readable-go-code
description: บทความ AI-proof Go (satire, 2026-04-08) สรุปเป็นกติกา 7 ข้อให้โค้ดทั้งคนและ AI อ่านออก — ชื่อ/comment ตรงโค้ด, ฟังก์ชันสั้น, ไม่ any/reflect, ไม่ init ข้าม package, มี test กัน refactor
metadata:
  type: reference
---

บทความเสียดสีของ huizhou92 (Level Up Coding) สอนวิธีทำโค้ด Go ให้ AI อ่านไม่ออก — ผู้ใช้เก็บเพื่อ**กลับด้านเป็นกติกา**: AI ตีความจาก *ชื่อ* กับ *comment* เป็นหลัก ถ้า 2 อย่างนี้หลอก AI จะพาไปผิดทางอย่างมั่นใจ

ย่อ 7 ข้อ: ชื่อสื่อความหมาย · comment บอก why และตรงโค้ด · ฟังก์ชันสั้น ไม่ `goto` · build tag/go:generate ต้องมีคำอธิบาย · concrete type แทน `any`/reflect/unsafe · wiring explicit ไม่ `init()` ข้าม package · โค้ดคล้ายกันที่ต่างโดยเจตนาต้องมี test ก่อนรับ refactor จาก AI

ตัวเลขในบทความ (เช่น comment ดี +30%) เป็นคำอ้างไม่มีที่มา

**Why:** ผู้ใช้สั่งเก็บเป็น memory + ส่งต่อ ptl_worker เป็น knowledge ทีม (2026-10-12)
**How to apply:** ใช้เป็นเกณฑ์ตอน review/เขียนโค้ด Go · ฉบับเต็มอยู่ `outbox/ai-readable-go-code.md` · prompt ส่งต่อทีมอยู่ `outbox/prompt-for-ptl-worker.md` · เกี่ยวกับ [[ptl-app-changes-go-to-ptl-worker-repo]]
