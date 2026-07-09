---
id: P017
title: "FMSUpdateAdapter — ShipmentProvider Priority Inversion in CreateUpdateStatusRequest"
date: 2026-06-22
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
severity: high
affected_components:
  - FMSUpdateAdapter.CreateUpdateStatusRequest
  - UpdateStatusShipToFMSV1.ShipmentProvider
related_decisions:
  - D022
related_snippets:
  - S022
---

# P017 — FMSUpdateAdapter: ShipmentProvider Priority Inversion

## Problem

`CreateUpdateStatusRequest` sets `ShipmentProvider` using an inconsistent, implicit priority chain across three code paths (weighted-item package block, normal-item package block, and the fallback outside the package block). The required business rule — Package > SubOrderItem > FulFillment.Carrier — is not explicitly enforced anywhere in the code.

## Root Cause

`thirdPartyLogistic` merges tiers 2 and 3 into a single variable before any package check runs:

```csharp
string thirdPartyLogistic = string.IsNullOrWhiteSpace(item.ThirdPartyLogistic)
    ? item.FulFillment.Carrier
    : item.ThirdPartyLogistic;
```

This collapses the distinction between tier 2 and tier 3, making it impossible to apply package-level override correctly in all branches. Additionally:

- The weighted-item branch (`package.ThirdPartyLogistic`) has no null guard and no fallback to `defaultCarrierCode`.
- The normal-item branch handles it partially correctly (`string.IsNullOrEmpty(package.ThirdPartyLogistic) ? defaultCarrierCode : package.ThirdPartyLogistic`) but inconsistently compared to the weighted-item branch.
- The two branches are asymmetric in their null handling, creating different runtime behavior for weighted vs normal items.

## Context

Activity Process Service — FMS adapter. Called during shipment status update flow. Carrier code resolution affects downstream FMS tracking and 3PL allocation.

## Constraints

- Brownfield codebase — existing class structure must not be broken
- No MediatR, no AutoMapper (.NET coding standards)
- Unit tests required in `result/` directory
- Priority order specified: Package > SubOrderItem.ThirdPartyLogistic > item.FulFillment.Carrier > defaultCarrierCode
