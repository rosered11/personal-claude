namespace RfidEventPlatform.EventProcessor.Transfers;

// D032: GateSession owns zero-loss / zero-delay / fail-safe invariants (DDD).
// ManifestSyncConsumer keeps IManifestCache warm via canonical event topics (EDA).
// D032 Addendum 3: match granularity per GTIN follows tracking_mode (Serialized |
// CountOnly, owned by Serialization Service) -- see MovementManifest below.
// No MediatR, no AutoMapper -- plain constructor DI per repo .NET standards.

public enum FailSafeMode
{
    Verified,    // manifest was present and fresh; verdict is a real match/mismatch
    FailOpen,    // no local manifest available; pass allowed, flagged for audit
    FailClosed   // no local manifest available; every tag alerted, flagged for audit
}

public enum GateVerdict
{
    Expected,      // Serialized GTIN: EPC found on the active MovementManifest
    Unexpected,    // Serialized GTIN: EPC not found on the manifest -- alarm immediately
    Unverifiable,  // no manifest available at all -- verdict driven by FailSafeMode
    Counted,       // CountOnly GTIN: EPC accepted toward its GTIN's running count --
                    // final judgment (match/mismatch) is deferred to Close(), since
                    // this mode reconciles a GTIN total, not a single EPC identity
    UnsupportedScheme // D032 Addendum 10: the tag's EPC Header does not match a
                    // GTIN-bearing scheme (SGTIN-96/198) -- distinct from
                    // Unexpected (a valid SGTIN just not on this manifest).
                    // Still counts as "evaluated" for zero-loss purposes; the
                    // read is never silently dropped, but GTIN/tracking_mode
                    // logic never runs on it since there is no GTIN to extract.
}

/// <summary>
/// D032 Addendum 10: only SGTIN-96 and SGTIN-198 are GTIN-bearing GS1 EPC
/// schemes -- every other scheme (SSCC, GRAI, GIAI, SGLN, ...) identifies a
/// different kind of thing entirely (shipping container, returnable/individual
/// asset, location) and structurally cannot join to item_master, no matter how
/// it's decoded. See manual/rfid-component-reference.md "EPC Tag Data
/// Standards" for the full reference table and reasoning.
/// </summary>
public sealed class UnsupportedEpcSchemeException : Exception
{
    public UnsupportedEpcSchemeException(string epc, byte header)
        : base($"EPC '{epc}' has Header 0x{header:X2}, which is not SGTIN-96 " +
               "or SGTIN-198 -- this platform only supports GTIN-bearing " +
               "schemes, since every downstream lookup (tracking_mode, " +
               "item_master enrichment) joins on GTIN.")
    {
    }
}

/// <summary>
/// Resolves the GTIN portion of an EPC. Must read the Header field FIRST to
/// determine scheme (SGTIN-96 vs SGTIN-198 -- see D032 Addendum 10) before
/// attempting to decode Company Prefix/Item Reference -- never assumes
/// SGTIN-96's bit layout blindly. Both schemes decode to a valid GTIN the same
/// way from here on; only the serial-number field's width/type differs between
/// them, which is irrelevant to this method. Throws
/// UnsupportedEpcSchemeException for any other scheme -- decoding one of those
/// as if it were SGTIN would not error, it would silently produce a
/// plausible-looking but meaningless "GTIN" from unrelated bits, which is worse
/// than a thrown exception.
/// </summary>
public interface IEpcGtinResolver
{
    /// <exception cref="UnsupportedEpcSchemeException">
    /// Header does not match SGTIN-96 or SGTIN-198.
    /// </exception>
    string ExtractGtin(string epc);
}

public sealed record MovementManifest(
    string ManifestId,
    long Version,              // D032 addendum: monotonic per ManifestId, orders updates
    string MovementType,      // "IntraSite" | "InterSite" | "InboundAsn" | "OutboundPick"
    string? PoRef,             // D032 Addendum 9: correlation key for the dock.appointment.confirmed
                                // join (Addendum 5/6) -- previously referenced in prose/diagrams as
                                // "join by PO ref" but never actually declared here, so the join could
                                // never have worked as documented. Populated by the supplier's ASN for
                                // InboundAsn, and by Ops/WMS at MovementManifest creation for
                                // IntraSite/InterSite (operations already has the PO/reference number
                                // at planning time). Nullable: a pure intra-site zone move may have no
                                // PO-like document at all, in which case dock-appointment correlation
                                // simply isn't available for that manifest -- movementRoundId-based
                                // resolution is unaffected either way.
    string SourceSiteId,
    string DestinationSiteId,
    IReadOnlySet<string> ExpectedEpcs,             // Serialized-mode GTINs: full expected EPC list
    IReadOnlyDictionary<string, int> ExpectedCountsByGtin, // CountOnly-mode GTINs: expected qty per GTIN
    int ExpectedEpcCount,      // D032 addendum: declared count, checked against ExpectedEpcs.Count
    string ExpectedEpcsChecksum, // D032 addendum: e.g. SHA-256 over sorted EPCs, hex-encoded
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string? GateId,                        // D032 Addendum 5: null until WMS/TMS dock appointment confirmed
    DateTimeOffset? ScheduledWindowStart,  // D032 Addendum 5
    DateTimeOffset? ScheduledWindowEnd)    // D032 Addendum 5
{
    /// <summary>
    /// D032 addendum: a manifest is only safe to cache/trust if what it claims
    /// (count, checksum) matches what it actually carries. Never bypassed --
    /// this is the single enforcement point that closes P027 Open Item 6
    /// regardless of which upstream failure produced the mismatch.
    ///
    /// Scope note (Addendum 3): this completeness proof currently covers
    /// ExpectedEpcs (the Serialized-mode list) only -- that is the payload most
    /// exposed to truncation. ExpectedCountsByGtin entries are small integers
    /// per GTIN line and are trusted as delivered; add a checksum here too if
    /// CountOnly ASN lines are ever found to need the same protection.
    /// </summary>
    public bool IsInternallyConsistent(Func<IReadOnlySet<string>, string> computeChecksum) =>
        ExpectedEpcs.Count == ExpectedEpcCount &&
        ExpectedEpcsChecksum == computeChecksum(ExpectedEpcs);

    /// <summary>
    /// tracking_mode is derived, not stored redundantly: a GTIN is CountOnly if
    /// (and only if) it has a declared expected count here. Avoids two parallel
    /// per-GTIN maps that could silently disagree.
    /// </summary>
    public bool IsCountOnlyGtin(string gtin) => ExpectedCountsByGtin.ContainsKey(gtin);

    /// <summary>
    /// D032 Addendum 5: true once WMS/TMS has confirmed a dock appointment for
    /// this manifest -- before that, GetActiveManifestForGate can never resolve
    /// it (nothing to correlate a physical gate pass to yet), so it stays
    /// invisible to inbound/outbound flows until the appointment lands, even
    /// though it may already be sitting in IManifestCache from pre-positioning.
    /// </summary>
    public bool HasGateAppointment =>
        GateId is not null && ScheduledWindowStart is not null && ScheduledWindowEnd is not null;
}

public sealed record EpcVerdict(
    string Epc, GateVerdict Verdict, FailSafeMode ModeApplied, DateTimeOffset EvaluatedAt);

/// <summary>
/// Per-GTIN reconciliation outcome for CountOnly-mode lines, computed once at
/// Close() -- unlike Serialized-mode EPCs, a CountOnly GTIN's correctness is a
/// property of the whole session, not of any single read.
/// </summary>
public sealed record GtinCountMismatch(string Gtin, int ExpectedCount, int ActualCount);

/// <summary>
/// D032 Addendum 4: closes a gap zero-loss does NOT cover. Zero-loss guarantees
/// every EPC the gate physically READ gets a verdict -- it says nothing about
/// expected EPCs that were never read at all (e.g. an entire pallet sent to the
/// wrong DC/PO, with nothing physically present to trigger an Unexpected
/// verdict). Computed once at Close(), symmetric to GtinCountMismatch but at
/// EPC granularity for Serialized-mode GTINs.
/// </summary>
public sealed record GateSessionResult(
    IReadOnlyList<EpcVerdict> Verdicts,
    IReadOnlyList<GtinCountMismatch> CountMismatches,
    IReadOnlySet<string> MissingExpectedEpcs);

/// <summary>
/// Local, edge-resident read model. Populated exclusively by ManifestSyncConsumer
/// (event-driven half of D032) -- GateSession never calls out to the Serialization
/// Service or any central registry synchronously.
/// </summary>
public interface IManifestCache
{
    MovementManifest? GetActiveManifestFor(string siteId, string movementRoundId);

    /// <summary>
    /// D032 Addendum 5 (closes P027 Open Item 2): resolves the active manifest
    /// by physical gate + time instead of a caller-supplied round id -- used by
    /// Inbound Auto-Receive / Outbound Pick-verify, where nothing tells
    /// GateSession which PO/ASN/Sales-Order applies to "this truck, right now"
    /// except the WMS/TMS dock appointment already bound to this gate_id and
    /// time window (see MovementManifest.HasGateAppointment).
    ///
    /// Returns null both when nothing is scheduled AND when more than one
    /// manifest's window overlaps this gate/time (ambiguous) -- both cases
    /// must fail-safe identically via the same FailSafeMode path GateSession
    /// already has for "no manifest." Which case occurred is an ops
    /// observability/alerting concern, not a decision GateSession should make.
    /// </summary>
    MovementManifest? GetActiveManifestForGate(string siteId, string gateId, DateTimeOffset asOf);
}

public interface IGateEventPublisher
{
    /// <summary>
    /// D032 Addendum 4: missingExpectedEpcs is inherently post-hoc -- it can only
    /// be known once the whole pass-through is over, unlike per-read verdicts.
    /// It must NOT drive the physical light-stack alarm (the truck/forklift has
    /// already moved on by the time this is known); it exists purely for this
    /// audit event, so downstream (e.g. the WMS Adapter before posting GRN) can
    /// react appropriately instead of silently trusting the ASN's declared count.
    /// </summary>
    void PublishTransferEvaluated(
        string sessionId, string gateId, string siteId,
        IReadOnlyList<EpcVerdict> verdicts, IReadOnlyList<GtinCountMismatch> countMismatches,
        IReadOnlySet<string> missingExpectedEpcs, FailSafeMode modeApplied);
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
    private readonly Dictionary<string, int> _gtinCounts = new();
    private readonly MovementManifest? _manifest;
    private readonly IEpcGtinResolver _gtinResolver;
    private readonly FailSafeMode _mode;
    private bool _closed;

    public string SessionId { get; }
    public string GateId { get; }
    public string SiteId { get; }
    public string? MovementRoundId { get; }

    /// <summary>
    /// D032 Addendum 5: movementRoundId stays the resolution key for Internal/
    /// Inter-site Transfer, where Ops/WMS already hands the physical mover a
    /// known round id at planning time. For Inbound Auto-Receive / Outbound
    /// Pick-verify, pass movementRoundId: null -- nothing at the gate knows a
    /// round id up front, so resolution instead falls to
    /// IManifestCache.GetActiveManifestForGate(siteId, gateId, openedAt),
    /// correlating via the WMS/TMS dock appointment already bound to this
    /// gate_id and time window. Same FailSafeMode fallback either way if
    /// resolution comes back null (not found, or ambiguous).
    /// </summary>
    public GateSession(
        string sessionId, string gateId, string siteId, string? movementRoundId,
        DateTimeOffset openedAt, IManifestCache manifestCache, IEpcGtinResolver gtinResolver,
        FailSafeMode fallbackModeWhenNoManifest)
    {
        SessionId = sessionId;
        GateId = gateId;
        SiteId = siteId;
        MovementRoundId = movementRoundId;
        _gtinResolver = gtinResolver;

        _manifest = movementRoundId is not null
            ? manifestCache.GetActiveManifestFor(siteId, movementRoundId)
            : manifestCache.GetActiveManifestForGate(siteId, gateId, openedAt);
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

        // D032 Addendum 3: every read still gets evaluated individually here --
        // CountOnly mode changes what the verdict MEANS, not whether one is
        // produced. This is why switching a GTIN to CountOnly saves ASN payload
        // size and DB/lifecycle overhead downstream, but saves zero work at the
        // gate itself: the zero-loss invariant requires a verdict per read either way.
        //
        // D032 Addendum 10: a foreign-scheme tag (SSCC on a pallet wrap read
        // alongside item-level SGTIN tags, for example) must not crash the whole
        // session -- zero-loss means EVERY read gets a verdict, including ones
        // GateSession cannot meaningfully evaluate. Caught here, not propagated.
        string gtin;
        try
        {
            gtin = _gtinResolver.ExtractGtin(epc);
        }
        catch (UnsupportedEpcSchemeException)
        {
            return new EpcVerdict(epc, GateVerdict.UnsupportedScheme, _mode, readAt);
        }

        if (_manifest.IsCountOnlyGtin(gtin))
        {
            _gtinCounts[gtin] = _gtinCounts.GetValueOrDefault(gtin) + 1;
            return new EpcVerdict(epc, GateVerdict.Counted, _mode, readAt);
        }

        var matched = _manifest.ExpectedEpcs.Contains(epc);
        return new EpcVerdict(
            epc, matched ? GateVerdict.Expected : GateVerdict.Unexpected, _mode, readAt);
    }

    /// <summary>
    /// D032 Addendum 3: reconciles CountOnly-mode GTINs against the manifest's
    /// declared quantities. Iterates the manifest's expected GTINs (not just
    /// ones actually seen) so a GTIN with zero reads still surfaces as a
    /// mismatch, not a silent pass.
    /// </summary>
    private IReadOnlyList<GtinCountMismatch> ReconcileCountOnlyGtins()
    {
        if (_manifest is null)
            return Array.Empty<GtinCountMismatch>();

        var mismatches = new List<GtinCountMismatch>();
        foreach (var (gtin, expected) in _manifest.ExpectedCountsByGtin)
        {
            var actual = _gtinCounts.GetValueOrDefault(gtin);
            if (actual != expected)
                mismatches.Add(new GtinCountMismatch(gtin, expected, actual));
        }
        return mismatches;
    }

    /// <summary>
    /// D032 Addendum 4 (P027 open item #9): the Serialized-mode counterpart to
    /// ReconcileCountOnlyGtins() above. Zero-loss only guarantees every EPC that
    /// was physically READ gets a verdict -- it says nothing about EPCs the
    /// manifest expected that were never read at all (a whole pallet sent to the
    /// wrong DC/PO, with no substitute tag physically present to trip
    /// Unexpected). Without this, that failure mode was silent: 400 read against
    /// an expected 500 produced 400 clean Expected verdicts and no signal that
    /// 100 were missing.
    /// </summary>
    private IReadOnlySet<string> ComputeMissingExpectedEpcs()
    {
        if (_manifest is null)
            return new HashSet<string>();

        var seenAndExpected = _verdicts
            .Where(kv => kv.Value.Verdict == GateVerdict.Expected)
            .Select(kv => kv.Key);
        return new HashSet<string>(_manifest.ExpectedEpcs.Except(seenAndExpected));
    }

    /// <summary>
    /// Zero-loss enforcement: closing is only allowed once every EPC the gate has
    /// physically seen has a recorded verdict. This turns "must never lose a tag"
    /// from a policy statement into a thrown exception if violated. Applies
    /// identically regardless of match granularity -- CountOnly reads still need
    /// a verdict (Counted) before Close() will proceed.
    ///
    /// Note this is a DIFFERENT guarantee from ComputeMissingExpectedEpcs() below:
    /// zero-loss is about not dropping what WAS read; missing-expected is about
    /// noticing what was never read despite being expected. Neither substitutes
    /// for the other.
    /// </summary>
    public GateSessionResult Close(IGateEventPublisher publisher)
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
        var countMismatches = ReconcileCountOnlyGtins();
        var missingExpectedEpcs = ComputeMissingExpectedEpcs();
        publisher.PublishTransferEvaluated(
            SessionId, GateId, SiteId, verdicts, countMismatches, missingExpectedEpcs, _mode);
        return new GateSessionResult(verdicts, countMismatches, missingExpectedEpcs);
    }
}

/// <summary>
/// Thrown when an incoming manifest event fails the completeness check (D032
/// addendum) -- the payload claims a count/checksum it does not actually
/// carry. Never caught silently: routes to the DLQ so it can be retried or
/// investigated, and is never allowed into IManifestCache.
/// </summary>
public sealed class ManifestIntegrityException : Exception
{
    public ManifestIntegrityException(string manifestId, long version)
        : base($"Manifest {manifestId} v{version} failed completeness check " +
               "(count/checksum mismatch) -- rejected, not cached.")
    {
    }
}

/// <summary>
/// Keeps IManifestCache warm at the correct edge (destination site for
/// inter-site movement, or destination DC for inbound ASN -- Addendum 3
/// reuses this same consumer for the source-tagged inbound flow).
///
/// D032 Addendum 2: this does NOT hold a Kafka subscription across the WAN --
/// that would repeat the exact mistake D033 ruled out for the Ingestion
/// Service hop (persistent per-site broker sessions at thousands-of-sites
/// scale, broker ports blocked on retail networks). Kafka never crosses the
/// WAN anywhere in this platform. Site & Config Service consumes
/// manifest.created/manifest.updated centrally, caches the latest valid
/// manifest per site in Redis, and exposes it over the same kind of
/// stateless HTTPS/mTLS poll contract edges already use for config
/// (GET /v1/manifests/pending?site_id=...&amp;since_version=...).
/// OnManifestEvent below is invoked by the edge-side poll-response
/// translator (HTTP client), not a broker consumer callback -- its logic is
/// otherwise unchanged from the original D032 design.
///
/// D032 Addendum 1 (P027 Open Item 6): a manifest is only ever cached here if
/// it (a) is internally consistent -- its declared count/checksum match what
/// it actually carries -- and (b) is not stale relative to whatever version
/// is already cached for the same ManifestId. This is the edge-side,
/// zero-trust check on what actually arrived over the WAN; Site & Config
/// Service separately runs the same validation centrally on what left Kafka
/// (defense in depth -- neither check substitutes for the other). Because
/// delivery is poll-based, a rejected manifest needs no DLQ/redelivery of its
/// own here -- the next poll cycle simply re-fetches.
///
/// D032 Addendum 3 (P027 Open Item #7): for InboundAsn manifests specifically,
/// nothing this consumer does establishes that the ASN's EPCs/quantities are
/// authentic -- it only guarantees the payload arrived intact and current.
/// The match at GateSession is a physical-reality-vs-supplier-claim
/// consistency check, not a verification against independent ground truth
/// (contrast with IntraSite/InterSite manifests, whose EPCs already have a
/// prior epc_registry row this platform itself created).
/// </summary>
public sealed class ManifestSyncConsumer
{
    private readonly IManifestCacheWriter _cacheWriter;
    private readonly IManifestDeadLetterPublisher _deadLetter;
    private readonly Func<IReadOnlySet<string>, string> _computeChecksum;

    public ManifestSyncConsumer(
        IManifestCacheWriter cacheWriter,
        IManifestDeadLetterPublisher deadLetter,
        Func<IReadOnlySet<string>, string> computeChecksum)
    {
        _cacheWriter = cacheWriter;
        _deadLetter = deadLetter;
        _computeChecksum = computeChecksum;
    }

    /// <summary>
    /// Called once per manifest returned in a GET /v1/manifests/pending poll
    /// response (translated 1:1 into this event shape by the edge HTTP
    /// client) -- not a Kafka consumer callback. See Addendum 2 above.
    /// </summary>
    public void OnManifestEvent(ManifestCreatedOrUpdatedEvent evt)
    {
        var incoming = evt.ToManifest();

        if (!incoming.IsInternallyConsistent(_computeChecksum))
        {
            // Truncated payload, source-side assembly bug, or transport corruption --
            // never cached, never trusted as Verified. Reuses the same DLQ + retry
            // skeleton every adapter in this platform already uses.
            _deadLetter.Publish(evt, new ManifestIntegrityException(incoming.ManifestId, incoming.Version));
            return;
        }

        var existing = _cacheWriter.TryGet(incoming.ManifestId);
        if (existing is not null && existing.Version >= incoming.Version)
        {
            // Stale or out-of-order delivery for the same ManifestId -- a manifest
            // can be internally consistent and still be superseded by a larger,
            // newer one. Idempotent event_id dedup alone does not protect against
            // this; discard silently, the newer version already won or is en route.
            return;
        }

        _cacheWriter.Upsert(incoming);
    }
}

public interface IManifestCacheWriter
{
    MovementManifest? TryGet(string manifestId);
    void Upsert(MovementManifest manifest);
}

/// <summary>
/// Edge-side implementation (D032 Addendum 2) is alerting/observability only --
/// NOT a redelivery queue. Delivery here is poll-based, so a rejected manifest
/// is naturally retried on the next poll cycle; this exists so a repeatedly
/// failing manifest is visible to ops rather than silently retried forever.
/// The equivalent central-side implementation, inside Site &amp; Config Service's
/// own Kafka consumer, is a real Kafka DLQ topic (manifest.dlq) -- that one
/// IS about redelivery/investigation, since it sits on a push (subscribe)
/// path where "just ask again next cycle" doesn't apply.
/// </summary>
public interface IManifestDeadLetterPublisher
{
    void Publish(ManifestCreatedOrUpdatedEvent evt, ManifestIntegrityException reason);
}

/// <summary>
/// D032 Addendum 5: published by WMS/TMS's dock scheduling function (via a new
/// reverse-sync responsibility on the existing WMS Adapter -- this is the
/// ONLY new integration surface this addendum requires) once a dock
/// appointment is confirmed for a PO. Consumed centrally by Serialization
/// Service, which joins it to the matching MovementManifest by PoRef (see
/// MovementManifest.PoRef -- added in Addendum 9; this join could not
/// actually have worked before that, since PoRef didn't exist as a field
/// until then) and republishes as a higher-Version ManifestCreatedOrUpdatedEvent
/// carrying GateId/window -- this event type itself never reaches
/// ManifestSyncConsumer or the edge.
///
/// D032 Addendum 6: arrival order versus the ASN is NOT guaranteed -- a dock
/// slot is commonly booked as soon as a PO is issued, which can be well
/// before the supplier submits the ASN this correlates to. If no
/// MovementManifest exists yet for PoRef when this event arrives,
/// Serialization Service stages it as a PendingDockAppointment (keyed by
/// PoRef) instead of dropping it, and consumes that staged record when the
/// ASN eventually creates the manifest -- populating GateId/window directly
/// into the first manifest.created rather than requiring a follow-up
/// manifest.updated. This staging is entirely internal to Serialization
/// Service; ManifestSyncConsumer/IManifestCache/GateSession never see it and
/// need no changes for either arrival order.
/// </summary>
public sealed record DockAppointmentConfirmedEvent(
    string EventId, string PoRef, string SiteId, string GateId,
    DateTimeOffset ScheduledWindowStart, DateTimeOffset ScheduledWindowEnd);

public sealed record ManifestCreatedOrUpdatedEvent(
    string EventId,
    string ManifestId,
    long Version,
    string MovementType,
    string? PoRef,                        // D032 Addendum 9: see MovementManifest.PoRef
    string SourceSiteId,
    string DestinationSiteId,
    IReadOnlyList<string> ExpectedEpcs,
    IReadOnlyDictionary<string, int> ExpectedCountsByGtin,
    int ExpectedEpcCount,
    string ExpectedEpcsChecksum,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string? GateId,                       // D032 Addendum 5: populated once WMS/TMS confirms
    DateTimeOffset? ScheduledWindowStart, // D032 Addendum 5
    DateTimeOffset? ScheduledWindowEnd)   // D032 Addendum 5
{
    public MovementManifest ToManifest() =>
        new(ManifestId, Version, MovementType, PoRef, SourceSiteId, DestinationSiteId,
            new HashSet<string>(ExpectedEpcs), ExpectedCountsByGtin, ExpectedEpcCount,
            ExpectedEpcsChecksum, CreatedAt, ExpiresAt,
            GateId, ScheduledWindowStart, ScheduledWindowEnd);
}
