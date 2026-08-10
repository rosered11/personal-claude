---
name: Treating secrets as a driven port (not just a rotation task)
description: How to frame committed-plaintext-secrets findings within an architecture lens rather than as a pure ops fix
type: project
---

When a problem tags `secrets-management` alongside an architectural lens (seen with Hexagonal
on Sprint-OMS), don't just recommend "move to Key Vault" as an ops aside — model it as a driven
port (`ISecretProvider`) implemented by swappable adapters (env-var adapter now, Key Vault/
Azure App Config adapter later). This keeps the recommendation inside the assigned lens's
vocabulary and gives DecisionSynthesizerAgent a code-level artifact instead of a bullet point.

Confirmed real committed plaintext secrets pattern in this codebase: MySQL `Uid=...;Pwd=...`
and Redis `password=...` directly inside `appsettings.json` (e.g.
`Portal/Portal.API/appsettings.json`). Constraint typically states these must be ROTATED, not
just deleted from git history — always call this out explicitly as a separate operational step
in `follow_up_considerations`, since architecture refactors alone don't rotate credentials.
