namespace RfidEventPlatform.EventProcessor.Transfers;

// D035: third GateSession resolution mode for warehouses with no dock-
// scheduling system (P030). Extends S032 (D032) -- GateSession's zero-loss/
// zero-delay/fail-safe invariants and completeness reconciliation are
// UNCHANGED. This file shows only what D035 adds: the new factory method,
// the new manifest-lifecycle field, the new pending-lookup port method, and
// the per-site correlation-mode config that lets D032 Addendum 5/6/7 keep
// serving dock-scheduled sites unchanged while this mode serves sites that
// have none. No MediatR, no AutoMapper -- plain constructor DI per repo
// .NET standards.

/// <summary>
/// D035: which inbound correlation mechanism a given site's receiving-zone
/// app should use. Delivered per-site through the existing Site & Config
/// Service heartbeat-push mechanism (ConfigPoller) -- a config value, not a
/// new system. Getting this wrong at a no-dock-scheduling site reproduces
/// the exact 100% FailSafeMode-fallback failure D035 exists to fix.
/// </summary>
public enum InboundCorrelationMode
{
    DockAppointment,  // D032 Addendum 5/6/7 -- unchanged, requires WMS/TMS dock scheduling
    ZoneReceiving      // D035 -- staff-selected ManifestId at a general receiving zone
}

public sealed record SiteCorrelationConfig(string SiteId, InboundCorrelationMode InboundMode);

/// <summary>
/// D035: one candidate a receiving-zone app can show staff when they select
/// a PO to start processing against. Deliberately minimal -- just enough for
/// a picklist UI to distinguish two pending deliveries for the same PoRef;
/// the actual disambiguation UI is a receiving-zone application concern, not
/// specified here (see P030 Open Item 2).
/// </summary>
public sealed record PendingManifestSummary(
    string ManifestId, string PoRef, DateTimeOffset CreatedAt, int ExpectedEpcCount);

/// <summary>
/// D035 additions to the D032 IManifestCache port. GetActiveManifestFor and
/// GetActiveManifestForGate (S032) are unchanged and still declared on the
/// same interface in the real codebase -- omitted here to keep this snippet
/// focused on what's new.
/// </summary>
public interface IManifestCacheZoneReceivingExtensions
{
    /// <summary>
    /// D035: read-only convenience lookup, NOT a new correlation mechanism.
    /// Returns every MovementManifest for this PoRef at this site that has
    /// not yet been consumed (ConsumedAt is null) and has not expired.
    /// Staff/the receiving-zone app select a ManifestId from this list --
    /// if exactly one result, the app may auto-select; if more than one,
    /// GateSession construction requires an explicit choice. Ambiguity is
    /// never silently resolved, mirroring D032 Addendum 5's treatment of
    /// overlapping dock-appointment windows.
    /// </summary>
    IReadOnlyList<PendingManifestSummary> GetPendingManifestsByPoRef(string siteId, string poRef);
}

/// <summary>
/// D035: marks a manifest consumed once a GateSession opened against it
/// closes successfully. Kept as a narrow port (not folded into
/// IManifestCacheWriter) since it is only ever called from GateSession.Close()
/// for the ZoneReceiving resolution mode -- the DockAppointment and
/// internal-transfer paths have no equivalent "pending picklist" to keep
/// clean, so they never call this.
/// </summary>
public interface IManifestConsumptionMarker
{
    void MarkConsumed(string manifestId, DateTimeOffset consumedAt);
}

/// <summary>
/// D035: extension methods demonstrating the new GateSession construction
/// path and its interaction with completeness/consumption tracking. In the
/// real codebase these would be members of the GateSession class itself
/// (see S032) -- expressed here as extensions purely so this snippet can be
/// additive to S032's code.cs without reproducing the whole file.
/// </summary>
public static class GateSessionZoneReceivingFactory
{
    /// <summary>
    /// D035: third named resolution path, alongside GateSession's existing
    /// constructor (movementRoundId, S032) and GetActiveManifestForGate
    /// (D032 Addendum 5). Resolves via the SAME generic
    /// IManifestCache.GetActiveManifestFor(siteId, key) method
    /// movementRoundId already uses -- manifestId is just a different kind
    /// of opaque, ops-known key, not a new lookup mechanism. FailSafeMode
    /// fallback is identical to every other resolution mode: a null result
    /// (not found, expired, already consumed) resolves to
    /// fallbackModeWhenNoManifest, exactly like the two existing paths.
    /// </summary>
    public static GateSession OpenForZoneReceiving(
        string sessionId,
        string siteId,
        string manifestId,
        DateTimeOffset openedAt,
        IManifestCache manifestCache,
        IEpcGtinResolver gtinResolver,
        FailSafeMode fallbackModeWhenNoManifest)
    {
        // GateSession's real constructor (S032) already accepts
        // movementRoundId as the "opaque key" resolution path -- D035 reuses
        // it directly rather than adding a fourth IManifestCache method.
        // gateId is not physically meaningful for zone receiving (there is
        // no FX9600 gate hardware at a general receiving zone, matching the
        // Store Backroom Inbound precedent from D032 Addendum 8), so it is
        // passed through as a logical zone identifier for audit purposes only.
        return new GateSession(
            sessionId: sessionId,
            gateId: $"zone:{siteId}",
            siteId: siteId,
            movementRoundId: manifestId,   // reuses the existing keyed-lookup path
            openedAt: openedAt,
            manifestCache: manifestCache,
            gtinResolver: gtinResolver,
            fallbackModeWhenNoManifest: fallbackModeWhenNoManifest);
    }

    /// <summary>
    /// D035: called after GateSession.Close() succeeds for a session opened
    /// via OpenForZoneReceiving, so the consumed manifest stops appearing in
    /// GetPendingManifestsByPoRef. Deliberately a separate, explicit step
    /// rather than baked into GateSession.Close() itself -- Close() (S032)
    /// has no reason to know about the ZoneReceiving-specific "pending
    /// picklist" concept; only the caller that used this factory method does.
    /// A missed call here is a UX/ops annoyance (stale picklist entries),
    /// never a zero-loss/fail-safe correctness break, since those invariants
    /// are enforced entirely inside GateSession.Close() itself, unchanged.
    /// </summary>
    public static void MarkManifestConsumedAfterClose(
        string manifestId, DateTimeOffset closedAt, IManifestConsumptionMarker marker)
    {
        marker.MarkConsumed(manifestId, closedAt);
    }
}

/// <summary>
/// D035: resolves which correlation mode a receiving-zone app should use for
/// a given site, from config delivered via the existing Site & Config
/// Service heartbeat push (same mechanism ConfigPoller already uses for
/// every other piece of edge config -- see D032 Addendum 2). This is the
/// switch that lets D032 Addendum 5/6/7 keep serving dock-scheduled sites at
/// full value while ZoneReceiving serves sites confirmed to have none.
/// </summary>
public sealed class SiteCorrelationModeResolver
{
    private readonly IReadOnlyDictionary<string, SiteCorrelationConfig> _configBySite;

    public SiteCorrelationModeResolver(IReadOnlyDictionary<string, SiteCorrelationConfig> configBySite)
    {
        _configBySite = configBySite;
    }

    /// <summary>
    /// Defaults to DockAppointment if a site has no explicit config entry --
    /// deliberately conservative, since that mode's failure signature
    /// (FailSafeMode fallback) is already well understood and monitored,
    /// whereas silently defaulting an unconfigured site to ZoneReceiving
    /// could mask a missing config entry as a working correlation path.
    /// </summary>
    public InboundCorrelationMode ResolveModeFor(string siteId) =>
        _configBySite.TryGetValue(siteId, out var config)
            ? config.InboundMode
            : InboundCorrelationMode.DockAppointment;
}
