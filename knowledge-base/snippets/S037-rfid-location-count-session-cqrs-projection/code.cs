namespace RfidEventPlatform.EventProcessor.LocationCounts;

// D037: location-scoped cycle count. Blends DDD (LocationCountSession, a
// GateSession-sibling aggregate reusing its zero-loss/zero-delay/fail-safe/
// completeness invariants) with CQRS (location_contents, a container-aware
// materialized projection folded from the same location-stamping events
// that already update epc_registry, fanned out edge-scoped exactly like
// every other GateSession-family cache). Does NOT touch the existing
// site-wide book-stock variance flow (count.completed / Event Processor
// flow #3) -- this is additive. No MediatR, no AutoMapper -- plain
// constructor DI per repo .NET standards.

/// <summary>
/// D037: granularity of the new epc_registry.LocationId column -- a
/// per-site config value (defaulting to Zone), mirroring D035's "config
/// value, not a new system" discipline. SiteId (already existing) answers
/// "which DC/store"; LocationId answers "where within that site."
/// </summary>
public enum LocationGranularity { Zone, Bin, Shelf }

/// <summary>
/// D037: write-side stamping port. Reuses the SAME event consumer that
/// already drives epc_registry status transitions from GateSessionResult-
/// shape events (Appendix 4 group 1) -- this is not a new write path
/// concept, just a new field (DestinationLocationId) that consumer also
/// applies when present. Flows with no location concept (outbound
/// pick-verify, POS sale) never populate it and never call StampLocation.
/// </summary>
public interface IEpcLocationWriter
{
    void StampLocation(string epc, string siteId, string locationId, DateTimeOffset at);

    /// <summary>Called on item.sold / tag.voided -- an EPC that left the
    /// building or was voided is not "at" any location.</summary>
    void ClearLocation(string epc, DateTimeOffset at);
}

/// <summary>
/// D037 (CQRS half): illustrative sketch of the projection-build query run
/// centrally in the Serialization DB (component #14) whenever a location's
/// contents need refreshing -- NOT executed at LocationCountSession
/// evaluation time, only when (re)building a snapshot to publish. UNION
/// folds two sources: EPCs stamped directly at the location, and EPCs
/// packed inside a container that is itself stamped at the location
/// (D036's container_contents table, joined for free -- both tables
/// already live in the same PostgreSQL database). This is what closes
/// P031 Open Item 1 for this flow BY CONSTRUCTION, at the baseline's
/// source, rather than relying on every downstream consumer remembering a
/// cross-reference rule.
/// </summary>
public static class LocationContentsProjectionSql
{
    public const string BuildQuery = @"
        SELECT epc, false AS via_container, NULL AS container_epc
        FROM epc_registry
        WHERE site_id = @siteId AND location_id = @locationId
        UNION ALL
        SELECT cc.item_epc AS epc, true AS via_container, cc.container_epc
        FROM epc_registry er
        JOIN container_contents cc ON cc.container_epc = er.epc
        WHERE er.site_id = @siteId AND er.location_id = @locationId;";
}

/// <summary>
/// D037 (CQRS half): the materialized read projection itself -- built
/// centrally, versioned, carrying the same completeness-proof fields
/// (D032 Addendum 1 pattern, reused verbatim) every pre-positioned
/// expected list on this platform already carries.
/// </summary>
public sealed record LocationContentsSnapshot(
    string SiteId,
    string LocationId,
    long Version,
    int ExpectedEpcCount,
    string ExpectedEpcsChecksum,
    IReadOnlySet<string> ExpectedEpcs,
    DateTimeOffset BuiltAt)
{
    public bool IsInternallyConsistent(Func<IReadOnlySet<string>, string> computeChecksum) =>
        ExpectedEpcs.Count == ExpectedEpcCount &&
        ExpectedEpcsChecksum == computeChecksum(ExpectedEpcs);
}

/// <summary>
/// D037 (CQRS half): edge-local port, deliberately parallel in SHAPE to
/// IManifestCache but NOT the same interface -- a location snapshot has no
/// Created->Distributed->Active->Consumed/Expired lifecycle (D032's
/// MovementManifest has that); it is continuously refreshed, never
/// "consumed." Conflating the two under one port would force a location
/// baseline to carry lifecycle semantics it does not have (see D037
/// decision text, point 4). Reuses the identical transport already proven
/// for MovementManifest and container_contents: Kafka (central-only) ->
/// Site & Config Service -> Redis -> HTTPS/mTLS poll -> edge cache.
/// Fanout is site-scoped -- an edge only ever receives snapshots for its
/// own site's locations, the tightest asymmetric-fanout scope this
/// platform has used yet (D036 established the pattern for containers).
/// </summary>
public interface ILocationContentsCache
{
    LocationContentsSnapshot? GetExpectedEpcsFor(string siteId, string locationId);
}

/// <summary>
/// D037 (DDD half): GateSession-sibling aggregate -- NOT a fifth
/// GateSession.OpenForXxx resolution mode. See D037 decision text point 4
/// for why: a live self-asserted snapshot is a structurally different kind
/// of "expected list" than a declared, eventually-consumed
/// MovementManifest, so it gets its own port rather than overloading
/// IManifestCache's fourth key. Reuses GateSession's proven invariant
/// SHAPE field-for-field: RecordRead/zero-loss, synchronous in-process
/// evaluation/zero-delay, explicit FailSafeMode, and Header-based scheme
/// dispatch (SGTIN unchanged, SSCC routes to container resolution exactly
/// as D036 specifies -- omitted here, see S036).
/// </summary>
public sealed class LocationCountSession
{
    private readonly string _siteId;
    private readonly string _locationId;
    private readonly LocationContentsSnapshot? _baseline;
    private readonly FailSafeMode _mode;
    private readonly Dictionary<string, GateVerdict> _verdicts = new();
    private readonly List<ContainerReadResult> _containerReads = new();

    private LocationCountSession(
        string siteId, string locationId, LocationContentsSnapshot? baseline, FailSafeMode mode)
    {
        _siteId = siteId;
        _locationId = locationId;
        _baseline = baseline;
        _mode = mode;
    }

    /// <summary>
    /// Opens against the container-aware projection, not a declared
    /// document. A null snapshot (never synced, or failed its own
    /// completeness check -- same zero-trust edge-side validation
    /// discipline D032 Addendum 1/2 established for polled manifests)
    /// resolves to fallbackModeWhenNoSnapshot, exactly like every other
    /// GateSession-family resolution path's fail-safe fallback.
    /// </summary>
    public static LocationCountSession Open(
        string siteId, string locationId, ILocationContentsCache cache,
        FailSafeMode fallbackModeWhenNoSnapshot)
    {
        var snapshot = cache.GetExpectedEpcsFor(siteId, locationId);
        var mode = snapshot is null ? fallbackModeWhenNoSnapshot : FailSafeMode.Verified;
        return new LocationCountSession(siteId, locationId, snapshot, mode);
    }

    /// <summary>Zero-loss: every uniquely-seen EPC gets recorded and
    /// evaluated -- identical contract to GateSession.RecordRead (S032).
    /// SSCC dispatch (D036) omitted here for brevity; see
    /// RecordContainerRead below for how a container read is tracked.</summary>
    public GateVerdict RecordRead(string epc, DateTimeOffset readAt)
    {
        var verdict = _baseline is not null && _baseline.ExpectedEpcs.Contains(epc)
            ? GateVerdict.Expected
            : _mode == FailSafeMode.FailOpen ? GateVerdict.Unverifiable : GateVerdict.Unexpected;

        _verdicts[epc] = verdict;
        return verdict;
    }

    public void RecordContainerRead(ContainerReadResult result) => _containerReads.Add(result);

    /// <summary>
    /// D037: the enforcement half of closing P031 Open Item 1 for this
    /// flow. An expected EPC that never got its own Expected verdict is
    /// NOT automatically flagged missing if it is ViaContainer in the
    /// baseline AND its owning container was itself read this session
    /// (ContainerRead, resolved) -- it is physically accounted for, just
    /// not individually read, exactly the case D036 warned about. An
    /// expected, ViaContainer EPC whose container was NOT read this
    /// session is NOT suppressed -- that container, and everything sealed
    /// inside it, is a real, legitimate absence a location count exists to
    /// catch.
    /// </summary>
    public IReadOnlySet<string> ComputeMissingExpectedEpcs(
        Func<string, (bool ViaContainer, string? ContainerEpc)> lookupProvenance)
    {
        if (_baseline is null) return new HashSet<string>();

        var readContainerEpcs = _containerReads
            .Where(c => c.ContentsResolved)
            .Select(c => c.ContainerEpc)
            .ToHashSet();

        var missing = new HashSet<string>();
        foreach (var expected in _baseline.ExpectedEpcs)
        {
            if (_verdicts.ContainsKey(expected)) continue; // physically read individually

            var (viaContainer, containerEpc) = lookupProvenance(expected);
            if (viaContainer && containerEpc is not null && readContainerEpcs.Contains(containerEpc))
                continue; // accounted for via a container confirmed present this session

            missing.Add(expected); // real gap -- never read, not covered by a read container
        }
        return missing;
    }
}
