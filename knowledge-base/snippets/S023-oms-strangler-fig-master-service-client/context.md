---
when_to_use: "Use this pattern when strangling an in-process project reference between two .NET services that are already deployed separately (independent pipelines, but coupled at compile-time via assembly reference). Applies when: (1) a big-bang network-boundary rewrite across all call-sites is too risky to do in one change, (2) the team already has an interface seam (like an IHandler-style port) that both an in-process and an HTTP implementation can satisfy, (3) each call-site needs to be migrated and rolled back independently via configuration rather than a code branch."
related_problems:
  - P018
related_decisions:
  - D023
language: "C#"
---

# S023 -- Strangler Fig Seam: IMasterServiceClient (Legacy In-Process vs Target HTTP)

## What This Solves

Order.API's project reference to Master today is an in-process call. D023 chose to strangle this
one seam at a time rather than cut over all call-sites simultaneously (the Microservices lens's
proposal). This snippet shows the seam for a single call-site (product lookup):

1. `IMasterServiceClient` is the port Order.API code depends on -- it does not know whether the
   call is in-process or over HTTP.
2. `InProcessMasterServiceClient` is the legacy adapter -- today's actual behavior, kept so other
   seams can keep working unmodified while this one seam is strangled.
3. `HttpMasterServiceClient` is the target adapter -- a real network hop, with Polly resilience
   (retry + circuit breaker) and W3C `traceparent` propagation so Gateway -> BFF -> Order -> Master
   is one continuous OpenTelemetry trace.
4. A per-seam configuration flag (`MasterService:Strangled:Product`) selects which implementation
   is registered in DI, so this one call-site can be flipped independently of any other seam
   (e.g. the Portal seam, or a pricing seam) and rolled back instantly by flipping the flag back.

No AutoMapper is used for the DTO mapping (repo-wide .NET standard forbids it) -- the mapping from
`MasterProductResponse` to `MasterProductDto` is an explicit method.

## Why Not Cut Over All Call-Sites At Once

The Microservices lens (rejected as the sole/immediate path in D023) would swap every project
reference to HTTP in one coordinated release. This snippet's per-seam flag is what makes the
Strangler Fig sequencing possible: each seam is independently testable, independently revertible,
and the blast radius of any one migration is a single call-site, not the whole Order-Master-Portal
boundary.
