---
name: memory-lives-in-the-repo
description: memory ทุกไฟล์ต้อง track บน git ได้ — ของจริงอยู่ใน repo ที่ .claude/memory/ ส่วน path ของ runtime เป็น junction ชี้มาที่นี่
metadata:
  type: feedback
---

**"ทุก memory ต้อง tracking บน git ได้"** — ผู้ใช้ขอไว้ 2026-09-21

ค่าเริ่มต้นของ Claude Code เก็บ memory ไว้ที่ `~/.claude/projects/<project>/memory/` ซึ่ง **อยู่นอก repo ⇒ git มองไม่เห็น** จึงจัดใหม่เป็น:

| | path |
|---|---|
| **ของจริง (git track)** | `D:\workspace\personal-claude\.claude\memory\` |
| path ที่ runtime ใช้ | `D:\Users\nukrihsana\.claude\projects\D--workspace-personal-claude\memory` → **directory junction** ชี้มาที่ข้างบน |

เขียนผ่าน path ไหนก็ได้ ไฟล์เดียวกัน · ที่นี่เป็นที่เดียวกับ `.claude/agent-memory/` ที่ repo track อยู่แล้ว (CLAUDE.md บอกว่า memory ของ agent เป็น project-level แชร์ผ่าน version control)

**How to apply**
- เขียน memory ใหม่ได้ตามปกติ แล้ว**เตือนให้ commit** — `git status` จะเห็นที่ `.claude/memory/`
- ถ้าย้ายเครื่อง/clone ใหม่ junction จะไม่มา ต้องสร้างใหม่:
  `New-Item -ItemType Junction -Path "<home>\.claude\projects\<project>\memory" -Target "<repo>\.claude\memory"`
  (junction ไม่ต้องใช้สิทธิ์ admin ต่างจาก symlink)
- อย่าสร้างสำเนาไว้ 2 ที่ — มันจะ drift แน่นอน
