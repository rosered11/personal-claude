"""
oms_resource_estimator.py  (KB snippet S039)

Back-of-the-envelope infrastructure sizing for Sprint-OMS (Order / Master / Portal /
Front / Report on AKS + Azure PostgreSQL Flexible Server + Azure Managed Redis +
OTel Collector -> Elasticsearch).

Method (D039): ONE workload model ("unit economics per order") feeds TWO topology
renderings so infra can see the trade-off explicitly:

    POOLED   (Modular-Monolith lens): one PG server, one Redis, one node pool, logical
             separation by database / key-prefix only. Lowest fixed cost, shared blast radius.
    ISOLATED (Microservices lens)   : one PG server per data domain, separate Redis per
             concern, per-service node pools. Highest fixed cost, smallest blast radius.

The recommended topology ("POOLED + resource-class carve-outs") is rendered as HYBRID:
pool by default, isolate only where two workloads need *conflicting resource classes*
(Redis eviction policy, DB durability class).

Every number below that is NOT read straight from the codebase is tagged [A#] and must be
replaced by a measured value (see 01-method-and-evidence.md section "Measure first").

Usage:  python oms_resource_estimator.py > 02-scenarios.md
Python 3.9+, standard library only.
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field

KB = 1024
GB = 1024 ** 3


# --------------------------------------------------------------------------------------
# 1. Workload model -- per-order unit economics
# --------------------------------------------------------------------------------------
@dataclass
class Workload:
    # [A1] API calls per order lifecycle. Webhook count (8-12) is from docs/oms/oms-database-scaling-plan.md 3.1.
    api_create: float = 1.0
    api_webhooks: float = 10.0
    api_ui_reads: float = 15.0            # [A2] Front/Portal reads per order (search, detail, timeline)
    outbound_jobs: float = 8.0            # [A3] Hangfire outbox jobs/order (create -> WMS+TMS; 3 transitions x TMS+Gateway)
    job_retry_rate: float = 0.05          # [A4]
    grpc_per_request: float = 2.0         # [A5] Master lookups per request (mostly cache hits)

    # DB row mix per order. Total ~30 rows = midpoint of the documented 20-40 (scaling plan 3.1).
    # (table, class, rows/order, avg bytes/row AFTER jsonb TOAST compression)  [A6]
    table_mix: tuple = (
        ("orders",                     "core",    1.0, 2048),
        ("order_lines",                "core",    1.4,  800),
        ("other core rows (customer/address/payment/release/...)", "core", 5.6, 500),
        ("order_status_history",       "history", 8.0,  300),
        ("order_webhook_logs",         "log",     8.0, 1800),   # raw_payload jsonb ~3KB raw
        ("outbox_dispatch_logs",       "log",     6.0, 3600),   # request+response jsonb ~6KB raw
    )
    index_bloat: dict = field(default_factory=lambda: {"core": 1.5, "history": 1.4, "log": 1.25})  # [A7]
    retention_days: dict = field(default_factory=lambda: {"core": 730, "history": 730, "log": 90})  # [A8] business decision
    horizon_days: int = 365               # plan storage for first 12 months of fill

    # LogDB (Sprint.Shared.Logs request/response log)
    logdb_rows_per_order: float = 34.0    # [A9] ~ every inbound + outbound HTTP call
    logdb_bytes_per_row: int = 1500       # [A10] UNKNOWN -- measure spc_log first
    logdb_retention_days: int = 30        # [A11]
    masterdb_gb: float = 10.0             # [A12] config + product master; grows with SKU count

    # CPU cost per call (ms of vCPU) [A13]
    cpu_ms_create: float = 80
    cpu_ms_webhook: float = 25
    cpu_ms_read: float = 6
    cpu_ms_job: float = 20
    cpu_ms_grpc: float = 2
    cpu_logging_overhead: float = 0.10    # Serilog -> LogDB batch
    cpu_otel_overhead: float = 0.05       # tracing @ sampled ratio, metrics
    job_latency_s: float = 0.6            # [A14] mean outbound WMS/TMS/Gateway call latency

    # service share of app CPU [A15]
    svc_share: dict = field(default_factory=lambda: {"Order": 0.45, "Portal": 0.25, "Front": 0.15, "Master": 0.10, "Report": 0.05})

    # Redis
    hangfire_bytes_per_job: int = 10 * KB  # [A16] job hash carries OutboxDispatchRequest incl. RequestPayload string
    hangfire_retention_h_default: float = 24.0  # Hangfire default succeeded-job expiry (verify on live Redis)
    hangfire_retention_h_tuned: float = 2.0
    redis_cache_mb: float = 300.0         # [A17] ICacheService config cache + output cache + token cache
    redis_cmds_per_order: float = 350     # [A18]

    # Observability (OTel -> ES). Matches D038/S038 method.
    log_events_per_order: float = 40
    log_event_bytes: int = 700            # [A19]
    spans_per_order: float = 200          # [A20] ~6 spans x 26 requests + jobs
    span_bytes: int = 500
    trace_sample_ratio: float = 0.10      # head sampling; errors kept via tail sampling at the collector
    metric_series: int = 3000             # [A21]
    metric_interval_s: int = 60
    metric_doc_bytes: int = 150
    es_retention_days: dict = field(default_factory=lambda: {"logs": 14, "traces": 7, "metrics": 30})
    es_replicas: int = 1
    es_overhead: float = 1.15


@dataclass
class Scenario:
    name: str
    sustained_orders_day: int
    peak_day_orders: int
    peak_hour_share: float


SCENARIOS = [
    Scenario("A  Launch",              10_000,  20_000, 0.10),
    Scenario("B  Design target",       50_000, 100_000, 0.10),
    Scenario("C  Growth (3x)",        150_000, 300_000, 0.10),
    Scenario("D  Campaign shock on B", 50_000, 300_000, 0.20),
]

BURST = 2.0         # [A22] intra-hour burst over the hourly mean
CPU_TARGET = 0.60   # target utilisation at peak


# --------------------------------------------------------------------------------------
# 2. Derived per-order numbers
# --------------------------------------------------------------------------------------
def per_order(w: Workload) -> dict:
    cls_bytes = {"core": 0.0, "history": 0.0, "log": 0.0}
    rows = 0.0
    for _, c, r, b in w.table_mix:
        cls_bytes[c] += r * b * w.index_bloat[c]
        rows += r
    api_calls = w.api_create + w.api_webhooks + w.api_ui_reads
    cpu_ms = (w.api_create * w.cpu_ms_create + w.api_webhooks * w.cpu_ms_webhook
              + w.api_ui_reads * w.cpu_ms_read + w.outbound_jobs * (1 + w.job_retry_rate) * w.cpu_ms_job
              + api_calls * w.grpc_per_request * w.cpu_ms_grpc)
    cpu_ms *= (1 + w.cpu_logging_overhead + w.cpu_otel_overhead)
    es_bytes = {
        "logs": w.log_events_per_order * w.log_event_bytes,
        "traces": w.spans_per_order * w.span_bytes * w.trace_sample_ratio,
    }
    return {
        "rows": rows, "api_calls": api_calls, "cls_bytes": cls_bytes, "cpu_ms": cpu_ms,
        "oltp_bytes": sum(cls_bytes.values()),
        "logdb_bytes": w.logdb_rows_per_order * w.logdb_bytes_per_row,
        "jobs": w.outbound_jobs * (1 + w.job_retry_rate), "es_bytes": es_bytes,
    }


# --------------------------------------------------------------------------------------
# 3. Catalogues (verified against Microsoft Learn, 2026-07 revision; re-check before purchase)
# --------------------------------------------------------------------------------------
PG_SKUS = [  # (vcpu, ram_gib, max_conn, max_iops)  General Purpose Ddsv5
    (2, 8, 859, 3750), (4, 16, 1718, 6400), (8, 32, 3437, 12800), (16, 64, 5000, 25600), (32, 128, 5000, 51200)]
DISK_STEPS = [(128, 500), (256, 1100), (512, 2300), (1024, 5000), (2048, 7500), (4096, 7500), (8192, 16000)]  # GiB, Premium SSD IOPS
REDIS_SKUS = [("B1", 1), ("B3", 3), ("B5", 6), ("B10", 12), ("B20", 24), ("B50", 60)]  # Azure Managed Redis Balanced


def pick_disk(gb_needed: float) -> tuple[int, int]:
    for size, iops in DISK_STEPS:
        if size >= gb_needed:
            return size, iops
    return DISK_STEPS[-1]


def pick_pg(vcpu_need: float, ram_need_gib: float, floor_vcpu: int) -> tuple:
    for sku in PG_SKUS:
        if sku[0] >= max(vcpu_need, floor_vcpu) and sku[1] >= ram_need_gib:
            return sku
    return PG_SKUS[-1]


def pick_redis(gb_needed: float) -> tuple:
    for name, gb in REDIS_SKUS:
        if gb >= gb_needed:
            return name, gb
    return REDIS_SKUS[-1]


# --------------------------------------------------------------------------------------
# 4. Per-scenario sizing
# --------------------------------------------------------------------------------------
def size(w: Workload, s: Scenario) -> dict:
    po = per_order(w)
    peak_orders_s = s.peak_day_orders * s.peak_hour_share / 3600
    req_s = peak_orders_s * po["api_calls"]

    # ---- app tier
    cores_busy = peak_orders_s * (po["cpu_ms"] / 1000) * BURST
    cores_req = cores_busy / CPU_TARGET
    pod_req = 0.5 if cores_req < 8 else 1.0
    pods, total_pod_req = {}, 0.0
    for svc, share in w.svc_share.items():
        n = max(2, math.ceil(cores_req * share / pod_req))
        pods[svc] = n
        total_pod_req += n * pod_req
    job_s = peak_orders_s * po["jobs"]
    slots_needed = job_s * w.job_latency_s * BURST
    order_pods = pods["Order"]
    slots_default = order_pods * 5          # Hangfire default = 5 x ProcessorCount (limit 1 vCPU -> 5)
    worker_count = min(40, max(10, math.ceil(slots_needed * 1.5 / order_pods)))
    node_vcpu_alloc = 4 * 0.8
    sys_overhead_vcpu = 2.0                  # ingress, OTel collector x2, kube-system share
    nodes = max(3, math.ceil((total_pod_req + sys_overhead_vcpu) / node_vcpu_alloc))

    # ---- PostgreSQL
    core_days = min(w.retention_days["core"], w.horizon_days)
    hist_days = min(w.retention_days["history"], w.horizon_days)
    log_days = min(w.retention_days["log"], w.horizon_days)
    n = s.sustained_orders_day
    oltp_gb = (n * po["cls_bytes"]["core"] * core_days + n * po["cls_bytes"]["history"] * hist_days
               + n * po["cls_bytes"]["log"] * log_days) / GB + w.masterdb_gb
    logdb_gb = n * po["logdb_bytes"] * w.logdb_retention_days / GB
    hot_gb = n * (po["cls_bytes"]["core"] + po["cls_bytes"]["history"]) * 7 / GB + n * po["cls_bytes"]["log"] * 1 / GB
    qps = req_s * 6.0 * BURST                # ~6 queries per request incl. EF round trips
    pg_cpu = qps * 1.0 / 1000 / 0.4          # 1 ms/query, 40% target util
    rows_s = peak_orders_s * po["rows"]
    iops_need = peak_orders_s * 40 * 0.3 + rows_s * 3.5 * 0.25 + qps * 0.02  # WAL flushes + checkpoint writes + read misses
    oltp_sku = pick_pg(pg_cpu, hot_gb * 2, 4)
    oltp_disk = pick_disk(oltp_gb * 1.3)
    log_sku = pick_pg(1, 4, 2)
    log_disk = pick_disk(logdb_gb * 1.3)
    pools = sum(pods.values()) * 2           # OrderDB/MasterDB + LogDB string per pod, roughly
    conn_default_worst = pools * 100         # Npgsql Maximum Pool Size default = 100 (no override found in repo)
    conn_recommended = pools * 30

    # ---- Redis
    jobs_day = s.peak_day_orders * po["jobs"]
    hf_default_gb = jobs_day * (w.hangfire_retention_h_default / 24) * w.hangfire_bytes_per_job / GB
    hf_tuned_gb = jobs_day * (w.hangfire_retention_h_tuned / 24) * w.hangfire_bytes_per_job / GB
    cache_gb = w.redis_cache_mb / 1024
    frag = 1.3
    redis_default = pick_redis((hf_default_gb + cache_gb) * frag / 0.65)
    redis_tuned = pick_redis((hf_tuned_gb + cache_gb) * frag / 0.65)
    redis_hf_iso = pick_redis(hf_tuned_gb * frag / 0.65)
    redis_cache_iso = pick_redis(cache_gb * frag / 0.65)
    redis_ops = peak_orders_s * w.redis_cmds_per_order * BURST

    # ---- Observability
    es_daily = {
        "logs": n * po["es_bytes"]["logs"] / GB,
        "traces": n * po["es_bytes"]["traces"] / GB,
        "metrics": w.metric_series * (86400 / w.metric_interval_s) * w.metric_doc_bytes / GB,
    }
    es_total_gb = sum(es_daily[k] * w.es_retention_days[k] for k in es_daily) * (1 + w.es_replicas) * w.es_overhead
    es_node_disk = pick_disk(es_total_gb / 3 * 1.3)[0]
    rec_per_s = peak_orders_s * (w.log_events_per_order + w.spans_per_order * w.trace_sample_ratio)
    collectors = 2 if rec_per_s < 3000 else math.ceil(rec_per_s / 3000)

    return dict(
        po=po, peak_orders_s=peak_orders_s, req_s=req_s, rows_s=rows_s, qps=qps,
        cores_busy=cores_busy, cores_req=cores_req, pod_req=pod_req, pods=pods, nodes=nodes,
        total_pod_req=total_pod_req, job_s=job_s, slots_needed=slots_needed, slots_default=slots_default,
        worker_count=worker_count, order_pods=order_pods,
        oltp_gb=oltp_gb, logdb_gb=logdb_gb, hot_gb=hot_gb, pg_cpu=pg_cpu, iops_need=iops_need,
        oltp_sku=oltp_sku, oltp_disk=oltp_disk, log_sku=log_sku, log_disk=log_disk,
        pools=pools, conn_default_worst=conn_default_worst, conn_recommended=conn_recommended,
        jobs_day=jobs_day, hf_default_gb=hf_default_gb, hf_tuned_gb=hf_tuned_gb, cache_gb=cache_gb,
        redis_default=redis_default, redis_tuned=redis_tuned, redis_hf_iso=redis_hf_iso,
        redis_cache_iso=redis_cache_iso, redis_ops=redis_ops,
        es_daily=es_daily, es_total_gb=es_total_gb, es_node_disk=es_node_disk, collectors=collectors, rec_per_s=rec_per_s,
    )


# --------------------------------------------------------------------------------------
# 5. Topology renderings (the two lenses)
# --------------------------------------------------------------------------------------
def pg_label(sku):  # (vcpu, ram, conn, iops)
    return f"{sku[0]} vCPU / {sku[1]} GiB"


def topologies(r: dict) -> dict:
    """Return fixed-footprint comparison: how many always-on units each topology implies."""
    oltp, logs = r["oltp_sku"], r["log_sku"]
    pooled = {
        "PostgreSQL servers": f"1 x {pg_label(oltp)} (HA) holding spc_oms+spc_master+spc_log; disk {pick_disk((r['oltp_gb'] + r['logdb_gb']) * 1.3)[0]} GiB",
        "Redis instances": f"1 x {r['redis_tuned'][0]} (cache + Hangfire share eviction policy)",
    }
    isolated = {
        "PostgreSQL servers": f"3 x HA servers: Order {pg_label(oltp)}, Master 2 vCPU/8 GiB, Log {pg_label(log_for_ha(r))}",
        "Redis instances": f"3 x (cache {r['redis_cache_iso'][0]}, Hangfire-Order {r['redis_hf_iso'][0]}, Hangfire-Master/Report B1)",
    }
    hybrid = {
        "PostgreSQL servers": f"2 servers: OLTP {pg_label(oltp)} HA (spc_oms+spc_master); LOG {pg_label(logs)} NO-HA (spc_log), {r['log_disk'][0]} GiB",
        "Redis instances": f"2 instances: cache {r['redis_cache_iso'][0]} (allkeys-lru) + Hangfire {r['redis_hf_iso'][0]} (noeviction)",
    }
    return {"POOLED": pooled, "ISOLATED": isolated, "HYBRID (recommended)": hybrid}


def log_for_ha(r):
    return pick_pg(1, 4, 2)


# --------------------------------------------------------------------------------------
# 6. Markdown rendering
# --------------------------------------------------------------------------------------
def fmt(x, nd=1):
    return f"{x:,.{nd}f}"


def render(w: Workload) -> str:
    po = per_order(w)
    res = [(s, size(w, s)) for s in SCENARIOS]
    out = []
    p = out.append
    p("# 02 - Scenario calculations (generated by oms_resource_estimator.py)\n")
    p("> Generated output. Do not edit by hand -- change the assumptions in the script and re-run.\n")
    p("## Per-order unit economics (the engine behind every number)\n")
    p("| Quantity | Value | Basis |")
    p("|---|---|---|")
    p(f"| DB rows written per order | {fmt(po['rows'])} | code: documented 20-40 (docs/oms/oms-database-scaling-plan.md 3.1), midpoint |")
    p(f"| OrderDB bytes per order (core / history / logs, incl. index+bloat) | {fmt(po['cls_bytes']['core']/KB)} / {fmt(po['cls_bytes']['history']/KB)} / {fmt(po['cls_bytes']['log']/KB)} KiB = **{fmt(po['oltp_bytes']/KB)} KiB** | [A6][A7] |")
    p(f"| LogDB (spc_log) bytes per order | {fmt(po['logdb_bytes']/KB)} KiB | [A9][A10] -- biggest unknown |")
    p(f"| Inbound API calls per order | {fmt(po['api_calls'],0)} | [A1][A2] |")
    p(f"| Hangfire outbox jobs per order | {fmt(po['jobs'])} | [A3][A4] |")
    p(f"| App CPU per order | {fmt(po['cpu_ms'],0)} ms | [A13] incl. logging+OTel overhead |")
    p(f"| ES bytes per order (logs / sampled traces) | {fmt(po['es_bytes']['logs']/KB)} / {fmt(po['es_bytes']['traces']/KB)} KiB | [A19][A20] |")
    p("")
    p("## Scenarios\n")
    p("| Scenario | Sustained orders/day (storage) | Peak-day orders (compute) | Peak-hour share | Peak orders/s | Peak API req/s | Peak DB rows/s |")
    p("|---|---|---|---|---|---|---|")
    for s, r in res:
        p(f"| {s.name} | {s.sustained_orders_day:,} | {s.peak_day_orders:,} | {int(s.peak_hour_share*100)}% | {fmt(r['peak_orders_s'],2)} | {fmt(r['req_s'],0)} | {fmt(r['rows_s'],0)} |")
    p("\nCross-check: scenario B peak = 2.78 orders/s vs. the 3-5 orders/s peak stated for 50K orders/day in P015 -- consistent.\n")

    p("## 1. AKS application tier\n")
    p("| Scenario | App vCPU busy @peak | vCPU requests (60% target) | Pods (Order/Portal/Front/Master/Report) | Pod request | Node pool (D4s_v5 = 4 vCPU/16 GiB) |")
    p("|---|---|---|---|---|---|")
    for s, r in res:
        pd = r["pods"]
        p(f"| {s.name} | {fmt(r['cores_busy'],2)} | {fmt(r['cores_req'],1)} | {pd['Order']}/{pd['Portal']}/{pd['Front']}/{pd['Master']}/{pd['Report']} | {r['pod_req']} vCPU req, 2x limit; 512Mi/1Gi (Report 1Gi/2Gi) | {r['nodes']} nodes across 3 zones |")
    p("\n### Hangfire outbox worker capacity (usually the first bottleneck, not CPU)\n")
    p("| Scenario | Outbox jobs/s @peak | Worker slots needed (jobs/s x 0.6s x burst) | Slots with DEFAULT WorkerCount (5 x 1 vCPU limit x Order pods) | Recommended explicit WorkerCount per Order pod |")
    p("|---|---|---|---|---|")
    for s, r in res:
        flag = " **SHORT**" if r["slots_default"] < r["slots_needed"] else ""
        p(f"| {s.name} | {fmt(r['job_s'],1)} | {fmt(r['slots_needed'],1)} | {r['slots_default']}{flag} | {r['worker_count']} |")

    p("\n## 2. PostgreSQL (Azure Database for PostgreSQL Flexible Server, General Purpose Ddsv5)\n")
    p("| Scenario | OLTP data after 12 mo (spc_oms+spc_master) | spc_log data (30d) | OLTP server | OLTP disk (Premium SSD) | LOG server | LOG disk | Peak IOPS need | Peak DB QPS |")
    p("|---|---|---|---|---|---|---|---|---|")
    for s, r in res:
        p(f"| {s.name} | {fmt(r['oltp_gb'],0)} GB | {fmt(r['logdb_gb'],0)} GB | {pg_label(r['oltp_sku'])} (HA) | {r['oltp_disk'][0]} GiB / {r['oltp_disk'][1]} IOPS | {pg_label(r['log_sku'])} (no HA) | {r['log_disk'][0]} GiB / {r['log_disk'][1]} IOPS | {fmt(r['iops_need'],0)} | {fmt(r['qps'],0)} |")
    p("\n### Connection budget (Npgsql default Maximum Pool Size = 100; no override found in repo)\n")
    p("| Scenario | Total pods | Worst-case connections (pods x 2 strings x 100) | OLTP max_connections | With Maximum Pool Size=30 |")
    p("|---|---|---|---|---|")
    for s, r in res:
        flag = " **EXCEEDS**" if r["conn_default_worst"] > r["oltp_sku"][2] else ""
        p(f"| {s.name} | {sum(r['pods'].values())} | {r['conn_default_worst']:,}{flag} | {r['oltp_sku'][2]:,} | {r['conn_recommended']:,} |")

    p("\n## 3. Redis (Azure Managed Redis, Balanced tier)\n")
    p("| Scenario | Hangfire jobs/peak day | Hangfire RAM @24h retention | Hangfire RAM @2h retention | Cache RAM | Redis ops/s @peak | ONE shared instance (24h) | ONE shared (2h) | HYBRID: Hangfire-only | HYBRID: cache-only |")
    p("|---|---|---|---|---|---|---|---|---|---|")
    for s, r in res:
        p(f"| {s.name} | {r['jobs_day']:,.0f} | {fmt(r['hf_default_gb'],2)} GB | {fmt(r['hf_tuned_gb'],2)} GB | {fmt(r['cache_gb'],2)} GB | {fmt(r['redis_ops'],0)} | {r['redis_default'][0]} ({r['redis_default'][1]} GB) | {r['redis_tuned'][0]} ({r['redis_tuned'][1]} GB) | {r['redis_hf_iso'][0]} | {r['redis_cache_iso'][0]} |")

    p("\n## 4. Observability (OTel Collector -> Elasticsearch -> Kibana)\n")
    p("| Scenario | Logs GB/day | Traces GB/day (sampled) | Metrics GB/day | ES total retained (x2 replica x1.15) | ES data nodes | Collector replicas |")
    p("|---|---|---|---|---|---|---|")
    for s, r in res:
        e = r["es_daily"]
        p(f"| {s.name} | {fmt(e['logs'],2)} | {fmt(e['traces'],2)} | {fmt(e['metrics'],2)} | {fmt(r['es_total_gb'],0)} GB | 3 x (4 vCPU / 16 GiB / {r['es_node_disk']} GiB) | {r['collectors']} x (0.5 vCPU / 512Mi-1Gi) |")
    p(f"\nRetention used: logs {w.es_retention_days['logs']}d, traces {w.es_retention_days['traces']}d, metrics {w.es_retention_days['metrics']}d. Feed these three sources into S038 (elastic_sizing_worksheet.py) for the final hot/warm/cold split.\n")

    p("## 5. Topology comparison per scenario (the two lenses + recommendation)\n")
    for s, r in res:
        p(f"### {s.name}\n")
        p("| Topology | PostgreSQL | Redis |")
        p("|---|---|---|")
        for name, t in topologies(r).items():
            p(f"| {name} | {t['PostgreSQL servers']} | {t['Redis instances']} |")
        p("")

    p("## 6. Marginal rule of thumb (per +10,000 sustained orders/day)\n")
    u = 10_000
    p("| Resource | Increment |")
    p("|---|---|")
    p(f"| OLTP storage (12-month horizon) | +{fmt(u*(po['cls_bytes']['core']+po['cls_bytes']['history'])*min(w.retention_days['core'],w.horizon_days)/GB + u*po['cls_bytes']['log']*w.retention_days['log']/GB,0)} GB |")
    p(f"| OLTP write rate | +{fmt(u*po['rows']/86400,1)} rows/s average |")
    p(f"| spc_log storage (30d) | +{fmt(u*po['logdb_bytes']*w.logdb_retention_days/GB,0)} GB |")
    p(f"| Hangfire Redis RAM (24h / 2h retention) | +{fmt(u*po['jobs']*w.hangfire_bytes_per_job/GB,2)} GB / +{fmt(u*po['jobs']*w.hangfire_bytes_per_job/GB/12,2)} GB |")
    p(f"| App vCPU (requests, at 10% peak hour) | +{fmt((u*2)*0.10/3600*po['cpu_ms']/1000*BURST/CPU_TARGET,2)} vCPU |")
    p(f"| Elasticsearch (retained, replica incl.) | +{fmt(u*(po['es_bytes']['logs']*w.es_retention_days['logs']+po['es_bytes']['traces']*w.es_retention_days['traces'])*(1+w.es_replicas)*w.es_overhead/GB,0)} GB |")
    return "\n".join(out)


if __name__ == "__main__":
    print(render(Workload()))
