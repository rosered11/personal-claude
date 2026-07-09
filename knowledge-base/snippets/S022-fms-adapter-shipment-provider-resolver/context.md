---
id: S022
title: "FMSUpdateAdapter — ResolveShipmentProvider Private Helper + Unit Tests"
language: C#
when_to_use:
  - When an adapter must resolve a carrier/provider field from multiple tiers with explicit priority
  - When two or more inner loop branches apply the same resolution logic asymmetrically
  - When a brownfield adapter has a business rule baked into a single variable that merges distinct tiers
related_problems:
  - P017
related_decisions:
  - D022
---

## Context

This snippet provides:
1. The `ResolveShipmentProvider` private helper method implementing the 3-tier priority chain.
2. The refactored inner loop body showing how `activePackage` is resolved before calling the helper (unified across weighted-item and normal-item branches).
3. Unit tests covering all 4 resolution scenarios (package wins, item wins, carrier wins, default wins) plus null-safety edge cases.

See `code.cs` for the implementation and `FMSUpdateAdapterTests.cs` for the unit tests.
