---
id: P026
title: "PTL Warehouse Integration -- Replace Manual Excel/File Exchange with API-Driven Task Orchestration"
date: 2026-08-10
tags: [warehouse-management, put-to-light, wms-sap-integration, mhe-plc-integration, api-integration, partial-fulfillment, task-orchestration, exception-handling]
related_decisions: [D031]
related_snippets: [S031]
---

# PTL Warehouse Integration -- Replace Manual Excel/File Exchange with API-Driven Task Orchestration

## Problem

The CMG Put-to-Light (PTL) process coordinates four independently-owned systems --
WMS, SAP, the PTL/MHE hardware controller, and Marketplace -- almost entirely through
manual Excel file exports/imports and manual SO/STO creation. The AS-IS flow diagram
(source: `CMG Put to Light - SPC.pptx`, slide 2) marks 7 distinct pain points with a
manual/❌ flag, concentrated on: Request Order / Pick-box driven by an Excel file from
the marketing team, manual export/import of stock, remaining, and SO/STO between
WMS <-> SAP <-> PTL, and a manual "cut LPN to match remaining" reconciliation step.
Only auto-pick and Marketplace status sync are automated end-to-end today. The target
(TO-BE) state must replace this batch/file coupling with real-time, API-driven task
generation, task confirmation (qty/carton/box status), and SO/STO creation, while
preserving partial-fulfillment (an SO/STO can be created before the full order/
allocation is picked) and adding validation for allocation-vs-stock mismatches and
cross-store carton mixing.

## Root Cause

The current integration mechanism is file-based batch exchange with no shared,
addressable state for an order/allocation/task's lifecycle across the four systems --
each system's local truth (WMS stock/remaining, SAP PO/SO/STO, PTL controller task/
carton status, Marketplace order status) is reconciled only periodically and manually
via spreadsheet export/import, which is exactly why partial fulfillment, mismatch
detection, and mixed-carton prevention cannot be enforced in real time today. There is
no process owner for the multi-step "allocation -> task -> confirm -> SO/STO -> remaining
sync" sequence; each of the 14 documented steps (spec slides 4-6) is a separate manual
or semi-manual hop.

## Summary

CMG's Put-to-Light fulfillment process is functionally correct but operationally manual:
warehouse staff manually select boxes from a marketing-supplied Excel file, manually
export/import stock and remaining figures between WMS and SAP, and manually create
SO/STO records from adjusted files. This is slow, error-prone (one documented step is a
manual LPN "cut to match remaining" adjustment), and structurally cannot support
partial SO/STO creation or automatic mismatch/exception handling because there is no
system with a live, cross-system view of task state. The spec's stated direction is to
replace every manual export/import/creation step with direct API integration across
WMS, SAP, the PTL/MHE controller, and Marketplace, while explicitly preserving partial
fulfillment and adding new validation for allocation-vs-stock mismatches and
cross-store carton mixing. This is a foundational fulfillment-path change with high
business impact (order accuracy, throughput, staff hours) and moderate technical risk
(4 external systems of record must interoperate without any one replacing another).

## Context

- **Source system**: CMG Put-to-Light (PTL) process, extracted from
  `inbox/push-to-light/CMG Put to Light - SPC.pptx` (8 slides; slides 7-8 are
  diagram/image-only, no extractable text -- see `inbox/push-to-light/spec-extracted.md`).
- **Systems involved**: Warehouse / Fulfillment Center (FC), WMS (stock, remaining),
  SAP (PO/SO/STO master + creation), PTL/MHE (Light + Merchandise hardware controller;
  task execution, qty/carton/box confirmation), Marketplace (MKP; order status sync,
  already automated today).
- **AS-IS flow** (slide 2): Export Stock (WMS) -> Import to SAP; Import stock/
  allocation -> Put to Light; Export SO/STO, Export remaining, Import remaining;
  Generate SO/STO; Background DO; Auto pick (automated); Auto Sync MKP status
  (automated); manual LPN-to-remaining cut; Request Order menu with business rule
  1 order = 1 box = 1 invoice; a PLT slot may hold multiple boxes but only 1 may be
  active per time period; Request Order/Pick-box driven by an Excel file from
  marketing (manual).
- **TO-BE flow** (slide 3) and 14-step current-vs-new table (slides 4-6): new screens
  for allocation import with validation/mapping/result-summary, two request-order
  modes (allocation-dependent / independent) with manual adjustment still allowed,
  automatic PTL task generation with a task-enquiry screen and pre-start edit/submit,
  explicit exception handling where stock > allocation or stock < allocation, API-based
  PTL integration (store mapping via API, send task by LPN+Item via API, get qty/store/
  carton confirmation via API with mixed-store cartons rejected as an error, get box
  status via API, LPN stock-deduction logic, end-to-end task status management),
  API-based SO/STO creation directly against SAP (replacing manual file import), and
  auto-pick modified to support partial SO/STO creation on request.
- **Stated TO-BE benefits** (slide 3): no manual export/import of FC stock or remaining,
  no manual SO/STO creation, and partially-created SO/STO possible without waiting for
  the whole order to be request-complete.
- **[MISSING]**: Slides 7-8 (diagram/image only) could not be extracted as text, so any
  additional flow detail they contain (likely a swimlane or sequence diagram) is not
  reflected here. No SLA/volume figures (orders/day, boxes/day) were provided in the
  extracted spec text.

## Constraints

- Must preserve the "1 order = 1 box = 1 invoice" invariant end-to-end across all four
  systems.
- Only one box may be "active" per PLT slot at any given time, even though a slot can
  hold multiple boxes.
- Must support partial SO/STO creation -- cannot require the full order/allocation to
  be request-complete before creating an SO/STO.
- Must reject (return an explicit error), not silently allow, any PTL task/carton that
  mixes items from more than one store.
- Must detect and explicitly handle allocation-vs-stock mismatches in both directions
  (stock > allocation and stock < allocation) as a defined exception path, not a silent
  failure.
- Must integrate with WMS, SAP, and the PTL/MHE controller via API rather than
  replacing any of them as the system of record for its own domain (stock, PO/SO/STO
  master, physical task execution respectively).
- LPN-level stock deduction and end-to-end task status must remain traceable/auditable,
  replacing today's manual "cut LPN to match remaining" step.

## Severity

high -- this is the core fulfillment path for CMG's Put-to-Light warehouse operation;
it is currently manual/Excel-driven end-to-end except for auto-pick and Marketplace
sync, which caps throughput and introduces order-accuracy risk (wrong box/invoice
pairing, cross-store carton mixing, undetected stock/allocation drift) as volume grows.

## Affected Components

- WMS (stock levels, remaining quantities)
- SAP (PO creation, SO/STO master and creation)
- PTL/MHE Controller (Light + Merchandise hardware; task send, qty/carton/box
  confirmation, acknowledgement)
- Marketplace integration layer (order status sync -- already automated)
- FC (Fulfillment Center) allocation-import, request-order/box-selection, and
  task-management screens (new/changed UI + API surface)
