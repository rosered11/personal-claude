"""
elastic_sizing_worksheet.py

Azure Elastic (Elasticsearch) capacity-sizing worksheet.

Methodology (D038): event-volume-driven tiered sizing (Event-Driven Architecture lens),
gated by a per-source CQRS index-scope review -- every candidate data source must be
explicitly classified as either FULL_MIRROR (indexed at source fidelity, e.g. an audit/
activity-log stream) or SCOPED_PROJECTION (only the fields a known query/filter/
aggregation pattern needs, e.g. a product-search read model) before its volume is
computed. This prevents the common "index everything by default" trap while still
producing a number even when query patterns for some sources are not yet defined.

Usage: populate SOURCES below with one entry per candidate index, then run this script.
Output is a per-source and total storage/shard estimate suitable for handing to the
infra team as an Azure Elastic (Elastic Cloud on Azure, or self-managed on AKS)
procurement input.
"""

from dataclasses import dataclass
from enum import Enum


class IndexScope(Enum):
    FULL_MIRROR = "full_mirror"              # EDA: durable, full-fidelity event/log sink
    SCOPED_PROJECTION = "scoped_projection"  # CQRS: query-driven, field-limited read model


@dataclass
class DataSource:
    name: str
    scope: IndexScope
    docs_per_day: int
    avg_source_doc_bytes: int
    retention_days: int
    hot_days: int              # days kept on fast/hot-tier nodes
    warm_days: int             # days kept on cheaper warm-tier nodes (0 if none)
    replica_count: int = 1
    # SCOPED_PROJECTION only: fraction of the source document actually indexed,
    # informed by a real query/field review -- 0.4 is a conservative placeholder
    # until that review happens (see constraint_notes in D038).
    projection_ratio: float = 0.4
    index_overhead_factor: float = 1.15  # inverted-index / doc-values overhead


TARGET_SHARD_SIZE_GB = 30  # midpoint of Elastic's own 10-50GB/primary-shard guidance


def raw_daily_bytes(src: DataSource) -> float:
    if src.scope is IndexScope.SCOPED_PROJECTION:
        return src.docs_per_day * src.avg_source_doc_bytes * src.projection_ratio
    return src.docs_per_day * src.avg_source_doc_bytes  # FULL_MIRROR: no reduction


def tier_storage_gb(src: DataSource) -> dict:
    """Split total retained storage across hot/warm/cold by the retention curve,
    then apply replica + index-overhead multipliers -- this is the number infra
    actually needs, since hot/warm/cold map to different Azure node SKUs/disks."""
    daily = raw_daily_bytes(src)
    cold_days = max(src.retention_days - src.hot_days - src.warm_days, 0)
    multiplier = (1 + src.replica_count) * src.index_overhead_factor

    def gb(days: int) -> float:
        return (daily * days * multiplier) / (1024 ** 3)

    return {
        "hot_gb": gb(src.hot_days),
        "warm_gb": gb(src.warm_days),
        "cold_gb": gb(cold_days),
    }


def recommend_primary_shards(hot_gb: float) -> int:
    """Elastic's own guidance: keep each primary shard between 10-50GB.
    Shard count is sized off the hot tier, since that is what active queries hit."""
    return max(1, round(hot_gb / TARGET_SHARD_SIZE_GB))


def report(sources: list[DataSource]) -> None:
    grand_total = {"hot_gb": 0.0, "warm_gb": 0.0, "cold_gb": 0.0}
    print(f"{'Source':30} {'Scope':18} {'Hot GB':>10} {'Warm GB':>10} {'Cold GB':>10} {'Shards':>7}")
    for src in sources:
        tiers = tier_storage_gb(src)
        shards = recommend_primary_shards(tiers["hot_gb"])
        for k in grand_total:
            grand_total[k] += tiers[k]
        print(f"{src.name:30} {src.scope.value:18} "
              f"{tiers['hot_gb']:>10.1f} {tiers['warm_gb']:>10.1f} {tiers['cold_gb']:>10.1f} {shards:>7}")

    print("-" * 90)
    print(f"{'TOTAL':30} {'':18} "
          f"{grand_total['hot_gb']:>10.1f} {grand_total['warm_gb']:>10.1f} {grand_total['cold_gb']:>10.1f}")
    print(f"\nTotal retained footprint: {sum(grand_total.values()):.1f} GB "
          f"(hot {grand_total['hot_gb']:.1f} GB drives node vCPU/RAM sizing; "
          f"warm/cold drive disk-tier/SKU choice, not vCPU)")


if __name__ == "__main__":
    # EXAMPLE ONLY -- replace with real per-source figures before handing to infra.
    # Every source below MUST first pass the CQRS scope-review question:
    # "does a known query pattern justify indexing less than the full document?"
    SOURCES = [
        DataSource(
            name="activity-log-stream",          # EDA: no query design done yet -> full fidelity
            scope=IndexScope.FULL_MIRROR,
            docs_per_day=2_000_000,
            avg_source_doc_bytes=800,
            retention_days=90,
            hot_days=7,
            warm_days=30,
            replica_count=1,
        ),
        DataSource(
            name="order-search-projection",      # CQRS: known query fields only
            scope=IndexScope.SCOPED_PROJECTION,
            docs_per_day=70_000,
            avg_source_doc_bytes=4_000,
            retention_days=365,
            hot_days=14,
            warm_days=90,
            replica_count=1,
            projection_ratio=0.25,
        ),
    ]
    report(SOURCES)
