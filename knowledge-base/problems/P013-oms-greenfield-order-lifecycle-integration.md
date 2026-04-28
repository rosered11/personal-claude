---
id: P013
title: "OMS Design — Greenfield Order Lifecycle Orchestrator with Multi-System Integration"
date: 2026-04-27
tags:
  - oms
  - order-management
  - microservices
  - distributed
  - integration
  - dotnet
  - postgresql
  - aks
  - event-driven
  - state-machine
  - cqrs
  - domain-driven-design
  - api
  - batch-processing
  - phased-rollout
severity: high
related_decisions:
  - D018
related_snippets:
  - S018
---

# P013 — OMS Design: Greenfield Order Lifecycle Orchestrator with Multi-System Integration

## Problem

The user must design and implement a greenfield Order Management System (OMS) that acts as the
central orchestrator for the order lifecycle across WMS (Warehouse Management), TMS (Transport
Management), POS (Point of Sale), and STS (Stock). The system must support both real-time online
events and batch integrations, handle a full order lifecycle (Booking → Picking → Delivery →
Invoicing → Payment), and support a phased store rollout via conditional routing.

## Root Cause

No existing OMS architecture. The developer is new to the OMS domain and has a Phase 1 integration
diagram from the solution team but lacks the architectural blueprint to implement it on .NET +
PostgreSQL + AKS.

## Context

Phase 1 system integration. Sprint Connect acts as integration middleware (proxy + file gateway).
Multiple downstream systems with different integration contracts:
- WMS: online events (Pick Started, Pick Confirmed, Product Pictures)
- TMS: booking/delivery flows (Request Time Slot, Create Booking, Out for Delivery, Delivered)
- POS: recalculation flows (POS Recalculation, ABB/Tax Invoice, Credit Note)
- STS: item master and financial documents via batch

Sprint Connect sits between Gateway and all downstream systems. Some Backend flows are marked
"Not roll out store" = phased rollout in progress. Phase 1 label = multi-phase delivery planned.

## Constraints

- Stack locked: .NET, PostgreSQL, AKS
- Sprint Connect is the integration layer — OMS does not call WMS/TMS/POS/STS directly
- Phase 1 scope: not all stores will be rolled out (conditional routing required)
- Both online and batch integration patterns must coexist
- Must handle concurrent order creation and state transitions
- Developer is new to OMS domain — design must be implementable without deep domain expertise upfront

## Affected Components

- OMS Service (greenfield)
- Sprint Connect (integration layer / middleware)
- WMS, TMS, POS, STS (downstream systems)
- PostgreSQL (order store)
- AKS (container platform)

## KB Search Results (at time of consultation)

Top matches by tag-intersection (all below 0.8 threshold — new problem class):
1. D003/P003 — ETL Per-Batch Transaction Scope — 0.111 (batch-processing, dotnet)
2. D012 — Distributed Transaction Strategy — 0.105 (distributed, microservices)
3. D005/P005 — ETL Batch Size + ChangeTracker — 0.100 (batch-processing, dotnet)
