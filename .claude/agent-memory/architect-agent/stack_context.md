---
name: Stack Context
description: Primary technology stack and default code language selection for problems in this codebase
type: project
---

# Primary Technology Stack

Derived from KOS incidents I1–I9, decisions D1–D21, and tech assets TA1–TA25.

## Services and Runtimes

| Technology | Role |
|-----------|------|
| .NET 8 / C# | ETL sync services, order API, EF Core data access |
| EF Core 8 | ORM for MySQL (Pomelo) and PostgreSQL (Npgsql) |
| Go | Distributed utility services (ID generation, consistent hashing) |
| Python | Airflow DAG callables, subprocess orchestration |
| Apache Airflow 2.x | DAG orchestration, PythonOperator, task timeout enforcement |
| Kafka | Event streaming (used for distributed event-driven patterns) |
| PostgreSQL | Primary RDBMS for order/inventory data |
| MySQL | Secondary RDBMS for SPC product/staging data |
| Redis | Rate limiting, caching, distributed counters |
| Prometheus + prometheus-net | Metrics collection, Histogram/Counter/Gauge |
| Grafana | Dashboard and alert visualization |
| Structured logging (Serilog/ILogger) | Airflow task logs + centralized log aggregation |

## Default Code Language by Problem Type

| Problem Tags | Default Language |
|-------------|-----------------|
| `ef-core`, `dotnet`, `etl`, `batch-processing` | C# |
| `postgresql`, `mysql`, `autovacuum`, `index-bloat` | SQL |
| `airflow`, `python`, `subprocess`, `orchestration` | Python |
| `distributed`, `id-generation`, `snowflake` | Go |
| `rate-limiting`, `redis` | Lua (Redis script) or Go |
| `distributed`, `transaction`, `saga` | C# (service layer) or SQL (schema) |

## Naming Conventions

- ETL services: `Sync{Entity}Jda.cs` or `Sync{Entity}{Source}To{Target}.cs`
- Airflow DAGs: `ds_{domain}_{sub_domain}.py`
- Staging tables: `Spc{Source}{Entity}Staging`
- Metrics: `etl_sync_{metric}_{unit}` with labels `sync_name`, `business_unit`
