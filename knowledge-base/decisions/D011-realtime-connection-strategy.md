---
id: D011
chosen_option: "Connection strategy selected by update interval and directionality"
tags: [websocket, sse, polling, real-time, architecture, api, selection]
related_snippets: []
---

# Decision: Real-Time Connection Strategy — WebSocket vs SSE vs Polling

## Context

Multiple features require server-to-client data push (order status, live dashboards, notifications). Defaulting to WebSocket for all cases adds stateful connection management complexity where simpler strategies suffice.

## Options Considered

1. **WebSocket** — full-duplex, stateful; required when client also sends data at high frequency.
2. **Server-Sent Events (SSE)** — server-push only, HTTP-based, automatic reconnect; simpler than WebSocket for one-directional push.
3. **Long polling** — universally compatible, no persistent connection; higher latency per update.
4. **Short polling** — simple; wastes requests when update rate is low.

## Decision

Select by update interval and directionality:

| Requirement | Strategy |
|-------------|----------|
| < 100ms latency, bidirectional | WebSocket |
| 100ms–1s, server-push only | SSE |
| > 1s acceptable, or behind proxy/firewall that blocks upgrades | Polling (interval = expected update rate) |

Default to SSE for dashboard and notification use cases — it requires no WebSocket upgrade, reconnects automatically, and is proxy-friendly.

## Consequences

- WebSocket reserved for genuinely bidirectional, low-latency cases (e.g., chat, live collaboration).
- SSE covers most "live dashboard" and notification requirements with simpler server infrastructure.
- Polling is always a valid fallback; tune interval to match expected data change rate.
