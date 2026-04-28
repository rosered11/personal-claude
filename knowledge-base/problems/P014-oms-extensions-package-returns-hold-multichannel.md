---
id: P014
title: "OMS Architecture Extensions — Package Tracking, Returns, Hold, and Multi-Channel Order Channels"
date: 2026-04-28
tags:
  - oms
  - order-lifecycle
  - ddd
  - aggregate
  - state-machine
  - returns
  - exception-handling
  - multi-channel
  - package-tracking
  - fulfillment
  - cqrs
  - outbox
  - domain-driven-design
  - dotnet
  - postgresql
severity: high
related_decisions:
  - D019
related_snippets:
  - S019
---

# P014 — OMS Architecture Extensions: Package Tracking, Returns, Hold, and Multi-Channel Order Channels

## Problem

The existing OMS design (P013/D018) covers the standard Delivery/ClickAndCollect/Express happy path
but has six structural gaps against the full OMS requirements specification:

1. **No Pack step** — design jumps from PickConfirmed directly to OutForDelivery, skipping the
   physical packing stage. TMS dispatch was triggered before packages were physically packed and
   TrackingIds assigned.

2. **No Package entity** — the original Shipment entity was a logical grouping misaligned with
   TMS's per-TrackingId reporting model, adding unnecessary indirection.

3. **No Returns & Reverse Logistics** — no modelled flow for customer return requests, TMS return
   pickup scheduling, or refund processing after Delivered.

4. **No OnHold/Release** — compliance checks, payment issues, or manual staff intervention had no
   way to pause the order lifecycle without cancelling it.

5. **Limited OrderChannels** — only Gateway, B2B, and Kiosk modelled. Marketplace (Shopee, Lazada,
   TikTok Shop), POSTerminal, BulkImport unsupported.

6. **No PackageLost exception path** — if TMS could not deliver a package, there was no domain path
   to hold the order, reassign, or escalate.

## Root Cause

The initial design was scoped to Phase 1 integration focusing on the standard Delivery happy path
derived from the solution team's integration diagram. The full OMS requirements specification —
covering the complete Pick→Pack→Ship lifecycle, post-delivery flows (returns, payment failure),
operational controls (hold/release), and the full multi-channel order origination surface — was not
available or not incorporated at Phase 1 design time.

## Context

Sprint Connect OMS bounded context. Existing D018 architecture (DDD aggregate + CQRS + Outbox) is
the foundation. WMS calls AssignPackages after PickConfirmed. TMS reports delivery events per
TrackingId (physical box), not per vehicle group. Returns require TMS involvement for pickup
scheduling. OnHold must be non-destructive with pre-hold state stored for resume. All new state
transitions must be idempotent via Outbox pattern.

## Constraints

- Sprint Connect remains the single bounded context owning the Order aggregate
- TMS reports per TrackingId, not per shipment/vehicle group
- WMS calls AssignPackages after PickConfirmed (WMS-initiated, not OMS-initiated)
- Returns require TMS involvement for return pickup scheduling
- OnHold must be non-destructive — order must resume from exact pre-hold state
- All new states must be idempotent — retry-safe via Outbox pattern
- Stack unchanged: .NET, PostgreSQL, AKS
- Must extend P013/D018 state machine — not replace it

## Affected Components

- OMS.Domain/Aggregates/Order.cs (aggregate extension)
- OMS.Domain/ValueObjects/Package.cs (new value object)
- OMS.Application/Commands/* (new command handlers)
- OMS.Infrastructure/Persistence/schema (new columns: pre_hold_state, packages JSONB)
- Sprint Connect integration layer (TMS return pickup ACL)
- TMS (return pickup scheduling callbacks)
- WMS (AssignPackages callback at PickConfirmed)

## KB Search Results (at time of consultation)

Top matches by tag-intersection — score below 0.8 threshold — new records created:
1. P013/D018/S018 — OMS Greenfield Design — overlap 0.35 (parent design; this extends it)
2. D015/P010 — MSSQL SEQUENCE + Idempotency — overlap 0.10 (idempotency pattern for new handlers)
3. D012 — Distributed Transaction Strategy — overlap 0.05 (Saga precedent for cross-service flows)

Note: although this problem is semantically an extension of P013, strict tag-intersection Jaccard
score is 0.35 (below 0.8 threshold), so new records were created rather than updating P013 in place.
P013/D018/S018 remain the authoritative Phase 1 baseline; P014/D019/S019 document the Phase 2
requirements extension.
