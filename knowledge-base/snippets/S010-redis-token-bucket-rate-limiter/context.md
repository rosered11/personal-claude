---
id: S010
slug: redis-token-bucket-rate-limiter
language: lua
when_to_use: "Use for API gateway or service-level rate limiting where burst tolerance is desirable (clients can accumulate tokens during idle periods and use them in a burst). Execute atomically via Redis EVAL to prevent race conditions under concurrent requests."
related_problems: []
related_decisions: [D013]
source: TA2
---

# Redis Token Bucket Rate Limiter (Lua / Redis)

Atomic rate limiting implemented as a Redis Lua script. A single `EVAL` call reads the current token count, refills based on elapsed time, and either grants or rejects the request — all in one atomic operation.

## Parameters

- `KEYS[1]` — bucket key (e.g., `rate_limit:user:{user_id}`)
- `ARGV[1]` — capacity (max burst size)
- `ARGV[2]` — refill_rate (tokens per second)
- `ARGV[3]` — current_timestamp (Unix ms)

## Return values

- `1` — request allowed (token consumed)
- `0` — request rejected (bucket empty)

## When NOT to use

- Strict per-window enforcement with no burst tolerance → use Sliding Window Counter instead
- Rate limit state must survive Redis restart → add `PERSIST` or use a persistent store
