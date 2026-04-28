---
id: S018
title: "OMS Order Aggregate (DDD State Machine) + CQRS CreateOrderHandler"
language: C#
when_to_use:
  - Implementing a greenfield OMS or any order lifecycle orchestrator
  - When you need a state machine enforced by a DDD aggregate root
  - When wiring DDD aggregates into CQRS command handlers with outbox pattern
  - As a starting point for any domain entity that has strict sequential state transitions
related_problems:
  - P013
related_decisions:
  - D018
---

# S018 — OMS Order Aggregate + CQRS CreateOrderHandler

## What this shows

1. **Order aggregate root** with a private state machine — all status transitions are enforced by
   the aggregate itself. No external code can call `Status = X` directly.
2. **Domain events** drained from the aggregate after each mutation.
3. **CreateOrderHandler** (CQRS command handler) wiring the aggregate, repository, outbox, and
   RolloutPolicy together.
4. **Idempotency check** (D015 pattern) — prevents duplicate orders on retry.
5. **Phased rollout gate** via RolloutPolicy domain service.

## How to extend

- Add `ConfirmBookingHandler`, `StartPickHandler`, `MarkDeliveredHandler` following the same
  handler pattern.
- Add `GetOrderStatusHandler` that reads from `order_status_view` (the CQRS read side).
- Add `SprintConnectAdapter` that subscribes to outbox events and calls Sprint Connect APIs.
