namespace RfidEventPlatform.EventProcessor.Transfers;

// D032: GateSession owns zero-loss / zero-delay / fail-safe invariants (DDD).
// ManifestSyncConsumer keeps IManifestCache warm via canonical event topics (EDA).
// No MediatR, no AutoMapper -- plain constructor DI per repo .NET standards.

public enum FailSafeMode
{
    Verified,    // manifest was present and fresh; verdict is a real match/mismatch
    FailOpen,    // no local manifest available; pass allowed, flagged for audit
    FailClosed   // no local manifest available; every tag alerted, flagged for audit
}

public enum GateVerdict
{
    Expected,      // EPC found on the active MovementManifest
    Unexpected,    // EPC not found on the manifest -- alarm immediately
    Unverifiable   // no manifest available at all -- verdict driven by FailSafeMode
}

public sealed record MovementManifest(
    string ManifestId,
    string MovementType,      // "IntraSite" | "InterSite"
    string SourceSiteId,
    string DestinationSiteId,
    IReadOnlySet<string> ExpectedEpcs,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record EpcVerdict(
    string Epc, GateVerdict Verdict, FailSafeMode ModeApplied, DateTimeOffset EvaluatedAt);

/// <summary>
/// Local, edge-resident read model. Populated exclusively by ManifestSyncConsumer
/// (event-driven half of D032) -- GateSession never calls out to the Serialization
/// Service or any central registry synchronously.
/// </summary>
public interface IManifestCache
{
    MovementManifest? GetActiveManifestFor(string siteId, string movementRoundId);
}

public interface IGateEventPublisher
{
    void PublishTransferEvaluated(
        string sessionId, string gateId, string siteId,
        IReadOnlyList<EpcVerdict> verdicts, FailSafeMode modeApplied);
}

/// <summary>
/// One instance per physical gate pass-through (one FX9600 gate "session").
/// Owns the zero-loss / zero-delay / fail-safe invariants from D032 directly --
/// these are not conventions the edge agent has to separately get right.
/// </summary>
public sealed class GateSession
{
    private readonly Dictionary<string, EpcVerdict> _verdicts = new();
    private readonly HashSet<string> _seenEpcs = new();
    private readonly MovementManifest? _manifest;
    private readonly FailSafeMode _mode;
    private bool _closed;

    public string SessionId { get; }
    public string GateId { get; }
    public string SiteId { get; }
    public string MovementRoundId { get; }

    public GateSession(
        string sessionId, string gateId, string siteId, string movementRoundId,
        IManifestCache manifestCache, FailSafeMode fallbackModeWhenNoManifest)
    {
        SessionId = sessionId;
        GateId = gateId;
        SiteId = siteId;
        MovementRoundId = movementRoundId;

        _manifest = manifestCache.GetActiveManifestFor(siteId, movementRoundId);
        // Fail-safe policy resolved ONCE, at session open, and stamped on every
        // verdict -- this is what makes a FailOpen pass auditable rather than silent.
        _mode = _manifest is not null ? FailSafeMode.Verified : fallbackModeWhenNoManifest;
    }

    /// <summary>
    /// Called once per unique EPC read event from the gate hardware. Safe to call
    /// multiple times for the same EPC within a session (multi-read is normal RFID
    /// behavior) -- duplicates never affect the verdict set, and no EPC is ever
    /// dropped from evaluation because of dedupe.
    /// </summary>
    public EpcVerdict RecordRead(string epc, DateTimeOffset readAt)
    {
        if (_closed)
            throw new InvalidOperationException($"GateSession {SessionId} is already closed.");

        _seenEpcs.Add(epc);

        if (_verdicts.TryGetValue(epc, out var existing))
            return existing; // duplicate read of an already-evaluated EPC -- no-op

        var verdict = Evaluate(epc, readAt);
        _verdicts[epc] = verdict;
        return verdict;
    }

    private EpcVerdict Evaluate(string epc, DateTimeOffset readAt)
    {
        if (_manifest is null)
        {
            // Zero-delay is trivially satisfied here: no manifest means no lookup
            // at all, just the pre-resolved FailSafeMode.
            var verdict = _mode == FailSafeMode.FailClosed
                ? GateVerdict.Unexpected
                : GateVerdict.Unverifiable;
            return new EpcVerdict(epc, verdict, _mode, readAt);
        }

        var matched = _manifest.ExpectedEpcs.Contains(epc);
        return new EpcVerdict(
            epc, matched ? GateVerdict.Expected : GateVerdict.Unexpected, _mode, readAt);
    }

    /// <summary>
    /// Zero-loss enforcement: closing is only allowed once every EPC the gate has
    /// physically seen has a recorded verdict. This turns "must never lose a tag"
    /// from a policy statement into a thrown exception if violated.
    /// </summary>
    public IReadOnlyList<EpcVerdict> Close(IGateEventPublisher publisher)
    {
        if (_closed)
            throw new InvalidOperationException($"GateSession {SessionId} is already closed.");

        var unevaluated = _seenEpcs.Where(epc => !_verdicts.ContainsKey(epc)).ToList();
        if (unevaluated.Count > 0)
        {
            throw new InvalidOperationException(
                $"GateSession {SessionId} cannot close: {unevaluated.Count} EPC(s) " +
                $"were read but never evaluated -- zero-loss invariant violated: " +
                $"[{string.Join(", ", unevaluated)}]");
        }

        _closed = true;
        var verdicts = _verdicts.Values.ToList();
        publisher.PublishTransferEvaluated(SessionId, GateId, SiteId, verdicts, _mode);
        return verdicts;
    }
}

/// <summary>
/// Event-driven half of D032: keeps IManifestCache warm at the correct edge
/// (destination site for inter-site movement) by subscribing to the canonical
/// manifest.created / manifest.updated topics, partitioned by destination site_id --
/// the exact "pre-position before physical arrival" mechanism the platform already
/// uses for serial-range pre-allocation and Site & Config's heartbeat-pushed config.
/// </summary>
public sealed class ManifestSyncConsumer
{
    private readonly IManifestCacheWriter _cacheWriter;

    public ManifestSyncConsumer(IManifestCacheWriter cacheWriter)
    {
        _cacheWriter = cacheWriter;
    }

    public void OnManifestEvent(ManifestCreatedOrUpdatedEvent evt)
    {
        // Idempotent by event_id, like every other platform event -- at-least-once
        // delivery is expected, duplicate upserts are harmless.
        _cacheWriter.Upsert(evt.ToManifest());
    }
}

public interface IManifestCacheWriter
{
    void Upsert(MovementManifest manifest);
}

public sealed record ManifestCreatedOrUpdatedEvent(
    string EventId,
    string ManifestId,
    string MovementType,
    string SourceSiteId,
    string DestinationSiteId,
    IReadOnlyList<string> ExpectedEpcs,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt)
{
    public MovementManifest ToManifest() =>
        new(ManifestId, MovementType, SourceSiteId, DestinationSiteId,
            new HashSet<string>(ExpectedEpcs), CreatedAt, ExpiresAt);
}
