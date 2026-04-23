---
id: D013
chosen_option: "Token Bucket for burst-friendly APIs; Sliding Window Counter for high-accuracy enforcement"
tags: [rate-limiting, token-bucket, sliding-window, redis, api, scalability, architecture]
related_snippets: [S010]
---

# Decision: Rate Limiter Algorithm Selection — Token Bucket vs Sliding Window Counter

## Context

API gateway and service-level rate limiting must handle burst traffic (legitimate client spikes) while preventing abuse. Two algorithms dominate: Token Bucket allows controlled bursting; Sliding Window Counter provides precise per-window enforcement without the boundary spike problem of Fixed Window.

## Options Considered

1. **Fixed Window Counter** — simple; allows 2× the limit at window boundaries (requests straddle two windows).
2. **Token Bucket** — allows burst up to bucket capacity; smooth sustained rate; memory-efficient; suited for API gateways.
3. **Sliding Window Log** — exact per-request accuracy; high memory (stores every request timestamp); impractical at scale.
4. **Sliding Window Counter** — approximates sliding window using two fixed-window buckets; high accuracy without log memory cost.

## Decision

Use **Token Bucket** (implemented in Redis Lua script — see S010) for external API gateways where burst tolerance is desirable. Use **Sliding Window Counter** for internal service-to-service rate limits where burst tolerance must be minimized and accuracy is critical.

Token Bucket parameters: `capacity = max_burst`, `refill_rate = sustained_rps`.

## Consequences

- Token Bucket: clients can burst up to `capacity` requests instantly after idle periods — desirable for SDKs retrying after backoff.
- Sliding Window Counter: accurate to within 1 window width; prevents boundary spikes; slightly more complex (two-bucket calculation).
- Both algorithms are implemented atomically in Redis Lua to prevent race conditions under concurrent requests.
- Choice is per-endpoint, not global — a single service may use both algorithms for different rate limit tiers.
