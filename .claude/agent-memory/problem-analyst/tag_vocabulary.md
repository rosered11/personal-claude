---
name: Tag Vocabulary
description: Canonical domain tags for problem analysis — use these exact strings when tagging problem records
type: project
---

# Canonical Domain Tags for Problem Analysis

Use these exact tag strings when extracting tags from raw problem descriptions. Do not invent new tags unless the domain is genuinely absent from this list.

## Technology

`ef-core` `dotnet` `go` `python` `postgresql` `mysql` `kafka` `airflow` `redis` `sqlalchemy` `subprocess` `threading` `prometheus`

## Performance / Data Access

`n+1` `connection-pool` `batch-processing` `etl` `transaction` `timeout` `memory` `oom` `changetracker` `compiled-query`

## PostgreSQL Specific

`autovacuum` `index-bloat` `maintenance` `storage`

## Code Quality / Process

`copy-paste` `correctness` `silent-failure` `testing` `dead-code` `debugging`

## Infrastructure / Orchestration

`orchestration` `airflow` `subprocess` `fk-constraint` `locale` `windows` `compatibility` `process`

## Architecture / Distributed

`distributed` `saga` `tcc` `consistency` `microservices` `architecture` `selection` `database` `api` `real-time` `websocket` `sse` `polling` `rate-limiting` `token-bucket` `sliding-window` `idempotency` `snowflake` `uuid` `id-generation` `event-driven` `cqrs`

## Observability

`observability` `monitoring` `metrics` `performance` `scalability`

## Severity Assignment

| Signal | Severity |
|--------|----------|
| Data loss / incorrect data committed | high |
| Service crash (OOM, timeout, process leak) | high |
| Performance degradation under load | high |
| Zero records processed without error | high |
| Debug/local environment issue only | medium |
| Config misconfiguration (not yet in production) | medium |
| Documentation / process gap | low |
