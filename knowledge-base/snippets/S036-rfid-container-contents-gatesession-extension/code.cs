namespace RfidEventPlatform.EventProcessor.Transfers;

// D036: container-level EPC (SSCC) modeling with a queryable relationship to
// the item-level EPCs packed inside. Extends S032 (D032) and S035 (D035) --
// GateSession's zero-loss/zero-delay/fail-safe invariants and completeness
// reconciliation (ComputeMissingExpectedEpcs, ReconcileCountOnlyGtins) are
// UNCHANGED. This file shows only what D036 adds: scheme classification on
// the resolver exception, a new ContainerRead verdict, an edge-local
// container-contents cache port (container -> contents direction ONLY --
// the reverse item -> container lookup is deliberately central-only, per
// D036's CQRS-borrowed fanout-scope discipline), and the GateSessionResult
// extension. No MediatR, no AutoMapper -- plain constructor DI per repo
// .NET standards.

/// <summary>
/// D036: the GS1 EPC schemes this platform's Header-validation logic must
/// distinguish. SGTIN-96/198 unchanged from D032 Addendum 10. Sscc96 is new
/// -- routed to container resolution instead of collapsing into
/// UnsupportedScheme. Grai/Giai/Sgln/Unknown are unchanged: still
/// UnsupportedScheme, per P031's Clarified Scope instruction not to weaken
/// that path.
/// </summary>
public enum EpcScheme
{
    Sgtin96,
    Sgtin198,
    Sscc96,
    Grai,
    Giai,
    Sgln,
    Unknown
}

/// <summary>
/// D036: UnsupportedEpcSchemeException now carries the classified scheme so
/// GateSession.Evaluate() can branch (Sscc96 -> container resolution;
/// everything else -> UnsupportedScheme, unchanged) without a second
/// Header-parsing pass. IEpcGtinResolver.ExtractGtin itself is unchanged --
/// it still throws for any non-GTIN-bearing scheme, exactly as D032
/// Addendum 10 specified; this is an additive field on the exception, not a
/// new resolver contract.
/// </summary>
public sealed class UnsupportedEpcSchemeException : Exception
{
    public EpcScheme Scheme { get; }

    public UnsupportedEpcSchemeException(string epc, byte header, EpcScheme scheme)
        : base($"EPC '{epc}' has Header 0x{header:X2}, classified as {scheme} -- " +
               "not a GTIN-bearing scheme, so ExtractGtin cannot produce a GTIN.")
    {
        Scheme = scheme;
    }
}

public enum GateVerdict
{
    Expected,
    Unexpected,
    Unverifiable,
    Counted,
    UnsupportedScheme,   // GRAI/GIAI/SGLN, or any other genuinely out-of-scope scheme -- unchanged
    ContainerRead         // D036: SSCC read, resolved (contents known locally) or unresolved
                           // (not yet synced / genuinely unknown) -- distinguished on EpcVerdict
                           // via ContainerResolved below, not via a second verdict value, so
                           // zero-loss bookkeeping ("every read has exactly one verdict") stays
                           // exactly as simple as every other verdict path.
}

/// <summary>
/// D036: one container EPC read, alongside its resolution outcome. Kept as a
/// separate record from EpcVerdict rather than overloading EpcVerdict's shape
/// -- a container read carries a contents count that no other verdict type
/// needs, and keeping it distinct avoids adding a nullable field to every
/// other verdict path for a case that only applies here.
/// </summary>
public sealed record ContainerReadResult(
    string ContainerEpc,
    bool ContentsResolved,
    int ResolvedContentsCount,
    FailSafeMode ModeApplied,
    DateTimeOffset EvaluatedAt);

/// <summary>
/// D036: edge-local, container -> contents direction ONLY. Populated by the
/// same ManifestSyncConsumer-style poll transport MovementManifest already
/// uses (Kafka -> Site & Config Service -> Redis -> HTTPS/mTLS poll -> edge
/// cache) -- no new transport mechanism. There is deliberately no matching
/// edge-side port for the reverse item -> container lookup: GateSession
/// never needs that direction, so it is never pushed to every site's
/// offline SQLite store. The reverse lookup is served centrally instead
/// (Query/Admin API, reading container_contents directly) -- D036's
/// CQRS-borrowed fanout-scope discipline.
/// </summary>
public interface IContainerContentsCache
{
    /// <summary>Returns null if this container EPC has not (yet) been
    /// synced locally -- same "absence, not an error" contract every other
    /// GateSession lookup already has.</summary>
    IReadOnlySet<string>? GetContentsFor(string containerEpc);
}

/// <summary>
/// D036: single write-side fact, deliberately dumb -- no aggregate, no
/// invariant beyond "did this event validate." Published either by the
/// supplier (extending the existing supplier-facing API at ASN-submission
/// time) or the DC/store Tagging Station App (at repack time). Validated at
/// the write boundary with a declared count + checksum, reusing D032
/// Addendum 1's completeness-proof pattern, before Serialization Service
/// ever persists it into container_registry/container_contents or lets it
/// reach the edge cache.
/// </summary>
public sealed record ContainerPackedEvent(
    string EventId,
    string ContainerEpc,
    string PackedBy,          // "Supplier" | "DC" | "Store" -- mirrors MovementType's role
    string SiteId,
    IReadOnlyList<string> ContainedItemEpcs,
    int DeclaredItemCount,
    string ContentsChecksum,  // e.g. SHA-256 over sorted ContainedItemEpcs, hex-encoded
    DateTimeOffset PackedAt)
{
    public bool IsInternallyConsistent(Func<IReadOnlyList<string>, string> computeChecksum) =>
        ContainedItemEpcs.Count == DeclaredItemCount &&
        ContentsChecksum == computeChecksum(ContainedItemEpcs);
}

/// <summary>
/// D036 extension to GateSession.Evaluate() (see S032 for the full method).
/// Shown here as the isolated branch this decision adds -- in the real
/// codebase this is inline inside the existing try/catch around
/// gtinResolver.ExtractGtin(epc), not a separate method.
/// </summary>
public static class GateSessionContainerEvaluation
{
    /// <summary>
    /// Called from GateSession.Evaluate()'s catch (UnsupportedEpcSchemeException)
    /// block. Sscc96 routes here; every other scheme (Grai/Giai/Sgln/Unknown)
    /// is unchanged and still returns GateVerdict.UnsupportedScheme directly,
    /// without ever calling this method -- see the caller-side branch note
    /// below.
    /// </summary>
    public static (GateVerdict Verdict, ContainerReadResult ContainerResult) EvaluateContainerRead(
        string epc, DateTimeOffset readAt, FailSafeMode sessionMode,
        IContainerContentsCache containerCache)
    {
        var contents = containerCache.GetContentsFor(epc);
        var result = contents is not null
            ? new ContainerReadResult(epc, true, contents.Count, sessionMode, readAt)
            : new ContainerReadResult(epc, false, 0, sessionMode, readAt);

        // Zero-loss: a container read always gets a verdict, resolved or not --
        // it is never silently dropped just because the local cache has not
        // synced it yet. An unresolved container behaves like any other
        // unresolved lookup: visible in the audit trail via sessionMode, not a
        // crash, not a special new fail-safe branch.
        return (GateVerdict.ContainerRead, result);
    }

    /// <summary>
    /// Caller-side dispatch sketch, showing how Evaluate()'s existing catch
    /// block (S032) now branches by classified scheme instead of collapsing
    /// every non-SGTIN read into one outcome.
    /// </summary>
    public static (GateVerdict Verdict, ContainerReadResult? ContainerResult) DispatchUnsupportedScheme(
        UnsupportedEpcSchemeException ex, string epc, DateTimeOffset readAt,
        FailSafeMode sessionMode, IContainerContentsCache containerCache)
    {
        if (ex.Scheme == EpcScheme.Sscc96)
        {
            var (verdict, result) = EvaluateContainerRead(epc, readAt, sessionMode, containerCache);
            return (verdict, result);
        }

        // Grai, Giai, Sgln, Unknown -- unchanged from D032 Addendum 10.
        return (GateVerdict.UnsupportedScheme, null);
    }
}

/// <summary>
/// D036 extension to GateSessionResult (see S032). ContainerReads is
/// additive -- a container read is reported on its own, never expanded into
/// synthetic per-item Expected verdicts for items the antenna did not
/// actually, individually read. See P031 Open Item 1: a correct
/// implementation must cross-reference this list against
/// MissingExpectedEpcs before treating an unread expected item as a real
/// shortage -- not implemented in this snippet.
/// </summary>
public sealed record GateSessionResultWithContainers(
    IReadOnlyList<EpcVerdict> Verdicts,
    IReadOnlyList<GtinCountMismatch> CountMismatches,
    IReadOnlySet<string> MissingExpectedEpcs,
    IReadOnlyList<ContainerReadResult> ContainerReads);
