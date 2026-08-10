---
when_to_use: "When a solution has multiple independently-deployed services that share a repo and a solution file, and you need to stop AI-assisted (or human) changes from silently deepening compile-time coupling across service boundaries -- run this as a dedicated build/test step in CI, in addition to (not instead of) the normal test suite."
related_problems: [P024]
related_decisions: [D029]
---

# Snippet: OMS Service-Boundary Fitness Function (NetArchTest)

## What this demonstrates

P024's audit found that Order.Core, Portal.Core, and Front.Core hold direct, actively-used
dependencies on other services' Infrastructure/Integration assemblies (e.g. Order.Core importing
Portal.Infrastructure.Interfaces.TMS.ITmsPostponeService directly, instead of owning its own port
and calling Portal over the gRPC seam that already exists for the same domain). Nothing in the
build today prevents this from growing with the next feature.

This snippet is a NetArchTest-based xUnit fixture that encodes the target boundary rule as an
executable, CI-enforced test: application code in `{Service}.Core` may depend on its own
`{Service}.Infrastructure`/`{Service}.Integration` and on `Shared.Infrastructure`, but must not
depend on another service's `*.Infrastructure` or `*.Integration` assembly. It ships with an
explicit, shrink-only allow-list seeded from the exact violations P024 found, so it can be adopted
immediately without breaking the current build, while making every new violation a compile-time
test failure instead of a silent `ProjectReference` addition.

This is the direct, concrete answer to "how do we prevent bad code from vibe coding going forward"
-- it does not depend on a developer or an AI assistant remembering a convention; it fails the
build the moment a new disallowed cross-service reference is introduced, and the allow-list itself
is a visible, reviewable artifact of technical debt that can only be edited downward.

## How to extend it

- Add one `[InlineData]` boundary rule per service pair as new seams are strangled (per D029's
  seam-by-seam rollout, starting with Front -> Order).
- Remove an allow-list entry only when the corresponding file has been migrated to use a
  locally-owned port + gRPC adapter instead of the direct Infrastructure reference.
- Point `Types.InAssembly` at the real compiled assemblies (via `Assembly.Load` or a project
  reference to each `*.Core` project) when wiring this into the actual Sprint-OMS solution --
  this snippet uses placeholder assembly names to stay portable as a reference implementation.
