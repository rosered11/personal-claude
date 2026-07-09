---
id: D022
title: "FMSUpdateAdapter — Private ResolveShipmentProvider Helper with Explicit 3-Tier Priority"
date: 2026-06-22
problem_id: P017
chosen_option: "In-Place Private Helper ResolveShipmentProvider (Layered Architecture)"
tags:
  - dotnet
  - correctness
  - priority-logic
  - adapter
  - shipment
  - fulfillment
  - null-safety
  - unit-testing
  - fms-adapter
  - activity-process
related_snippets:
  - S022
---

# D022 — FMSUpdateAdapter: Private ResolveShipmentProvider Helper

## Decision

Extract a private `ResolveShipmentProvider(PackageTbMessage, SubOrderItemMessage, string)` helper method inside `FMSUpdateAdapter`. Both the weighted-item and normal-item inner loops call this single method. The method implements the required 3-tier priority chain with explicit null guards at each tier.

## Lenses Evaluated

- **Hexagonal Architecture** — Extract `IShipmentProviderResolver` port + implementation. Clean separation, highly testable in isolation.
- **Layered Architecture** — In-place private helper, zero structural change to class hierarchy.

## Rationale

Layered wins for this problem because:

1. **Single call site** — `FMSUpdateAdapter` is the only consumer of this logic. An `IShipmentProviderResolver` interface adds DI registration cost for zero reuse benefit at this time.
2. **Minimum footprint** — The fix is a correctness bug in an existing adapter; the risk of introducing new structural abstractions into a brownfield codebase is higher than the risk of the simpler approach.
3. **Branch unification** — The private helper eliminates the asymmetry between weighted-item and normal-item branches. Both call `ResolveShipmentProvider` identically, removing the maintenance divergence.
4. **Hexagonal contribution absorbed** — The Hexagonal lens contributed one improvement: explicit named-tier comments (Priority 1/2/3) and keeping `thirdPartyLogistic` assignment separate from the package override. This makes the rule visible to any future reader without requiring an interface.
5. **Null safety corrected** — The weighted-item package path previously assigned `package.ThirdPartyLogistic` with no null guard; the helper enforces `IsNullOrWhiteSpace` at every tier before advancing.

## Tradeoffs Accepted

- Priority rule is not expressed as a formal named contract (no interface). If a second adapter needs this logic, extraction to an `IShipmentProviderResolver` should be revisited at that point.
- Unit tests require constructing `SubOrderItemMessage` and `PackageTbMessage` fixtures rather than testing the resolver in isolation.

## Priority Chain (Canonical)

```
1. package != null && package.ThirdPartyLogistic not empty  → use package.ThirdPartyLogistic
2. item.ThirdPartyLogistic not empty                         → use item.ThirdPartyLogistic
3. item.FulFillment?.Carrier not empty                       → use item.FulFillment.Carrier
4. (all empty)                                               → use defaultCarrierCode
```
