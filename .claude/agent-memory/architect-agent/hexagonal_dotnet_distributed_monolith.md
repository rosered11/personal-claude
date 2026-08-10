---
name: Hexagonal Architecture for .NET distributed-monolith audits
description: Recurring evidence pattern and fix shape when Hexagonal lens is assigned to tags like distributed-monolith/microservices/grpc/dotnet/vibe-coding
type: project
---

Observed in Sprint-OMS (Order/Portal/Master/Front/Report, .NET 10, FastEndpoints, EF Core/Postgres, gRPC):
ports (interfaces, DTOs, .proto contracts) and driven adapters (EF Core DbContext, entities,
repositories) are colocated inside a single `{Service}.Infrastructure` project. Consumers then
take a direct `ProjectReference` to that whole assembly instead of a slim contracts package,
which is the concrete mechanism behind "distributed monolith" complaints in AI-vibe-coded
multi-service .NET systems.

**Why this matters for the lens:** When assigned Hexagonal Architecture against this tag
combination, always verify coupling by reading `.csproj` `ProjectReference` entries directly
(don't trust the problem summary) — e.g. found `Front.API` referencing `Order.Infrastructure`
directly, and `Order.API`/`Portal.Integration` referencing `Master.Infrastructure` directly.
This is stronger, more specific evidence than "some cross-service calls use direct project
references" from the problem JSON, and makes pros/cons concrete instead of generic.

**How to apply:** The clean, low-disruption option is a "Contracts assembly extraction" —
move ports/DTOs to a new `{Service}.Contracts` project while KEEPING existing namespaces/type
names unchanged (only the assembly boundary moves), so gRPC/interface consumers don't need code
changes, only a `ProjectReference` swap. Pair this with a fitness function (e.g. NetArchTest
rule: "no *.API/*.Integration project may reference another service's *.Infrastructure
assembly") to make the boundary durable — this directly answers the "no enforced architectural
governance layer" root cause that shows up repeatedly in these audits.

Also check for missing `*.Tests.csproj` per service before promising "non-disruptive" — if
under half the services have test projects, flag it explicitly as a con/risk rather than
assuming CI safety nets exist.
