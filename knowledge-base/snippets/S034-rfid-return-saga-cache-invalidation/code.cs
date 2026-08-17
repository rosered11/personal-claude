namespace RfidEventPlatform.EventProcessor.Returns;

// D034: ReturnSaga owns the multi-step return process (Saga Pattern) --
// verify -> inspect -> refund-authorize/compensate -- with locality deciding
// how verification is reached: SameStore resolves entirely from local state
// (zero network, matching checkout/EAS's existing offline guarantee);
// CrossStore is the platform's first and only deliberate, scoped exception
// to "no synchronous registry calls from site operations." Paid-EPC cache
// invalidation is always event-driven (EDA folded in as transport),
// partitioned by the ORIGINATING site_id, never a fleet-wide broadcast.
// No MediatR, no AutoMapper -- plain constructor DI per repo .NET standards.

public enum ReturnLocality
{
    SameStore,   // returning to the store that made the original sale
    CrossStore   // returning to a different store than the one that sold it
}

public enum ReturnVerdict
{
    Verified,            // proof of a legitimate prior sale was established
    PendingVerification, // D034: third fail-safe outcome -- item accepted,
                          // refund deferred, distinct from GateSession's
                          // binary FailOpen/FailClosed (see class doc below)
    Rejected             // proof could not be established -- no prior sale
                          // evidence anywhere the saga is allowed to check
}

public enum InspectionOutcome
{
    Resellable, // passes condition check at the counter -> store_stock
    Damaged,    // terminal exception branch, parallel to `voided`
    FraudHold   // TID mismatch, or employee judgment -- withheld from resale
                // AND from paid-EPC cache invalidation until LP resolves it
}

/// <summary>
/// D034: result of the one deliberate, bounded synchronous checkpoint this
/// platform allows -- CrossStore return verification against Serialization
/// Service. Never called for SameStore returns, and never called from
/// checkout, EAS, or GateSession -- this port exists ONLY for ReturnSaga's
/// CrossStore branch.
/// </summary>
public sealed record CentralVerificationResult(
    bool SaleConfirmed, bool TidBindingOk, string? OriginatingSiteId);

public interface ICentralReturnVerifier
{
    /// <summary>
    /// Bounded by a caller-supplied timeout -- ReturnSaga treats a timeout
    /// identically to an unreachable central service: both resolve to
    /// PendingVerification, never to a silent Verified or a blind Rejected.
    /// </summary>
    Task<CentralVerificationResult?> TryVerifyAsync(
        string epc, string? tid, TimeSpan timeout, CancellationToken ct);
}

/// <summary>
/// Local, edge-resident read model -- the SAME paid-EPC cache checkout/EAS
/// already read/write, reused here rather than a parallel structure.
/// SameStore returns resolve entirely against this: presence in the local
/// cache IS the proof of a legitimate local sale, no network required.
/// </summary>
public interface IPaidEpcCache
{
    bool Contains(string epc);
    void Remove(string epc);
}

/// <summary>
/// D034: locally cached TID bindings for items tagged/sold at THIS store
/// (backroom tagging already populates this at encode time) -- lets a
/// SameStore return check tid_registry without any network call, mirroring
/// how IPaidEpcCache lets it check "was this sold here" locally.
/// </summary>
public interface ILocalTidBindingCache
{
    bool TryGetBoundTid(string epc, out string? tid);
}

/// <summary>
/// D034: CountOnly GTINs deliberately do not persist a full per-EPC
/// lifecycle (Dual EPC Tracking Mode decision) -- without this, a CountOnly
/// return has NO ground truth to validate against, for either locality.
/// This ledger is intentionally short-lived: a lightweight (epc, gtin,
/// site_id, sold_at) record, purged after a configurable return-window
/// retention period, NOT a reversal of CountOnly's storage-saving premise.
/// </summary>
public sealed record SoldEpcLedgerEntry(
    string Epc, string Gtin, string SiteId, DateTimeOffset SoldAt);

public interface ISoldEpcLedger
{
    /// <summary>
    /// Returns null if no record exists -- either never sold, or the return
    /// window has already expired and the record was purged. Either case
    /// falls back to the store's existing non-RFID exception process
    /// (receipt-based manual approval), not a new architectural mechanism.
    /// </summary>
    SoldEpcLedgerEntry? TryGet(string epc);
}

/// <summary>
/// Published once ReturnSaga reaches Resellable or Damaged -- the only two
/// outcomes that conclude the item is no longer legitimately "paid and in
/// circulation" without further dispute. Partitioned by OriginatingSiteId
/// (the store whose cache actually added this EPC at sale time), NEVER
/// broadcast to every store -- only the one store whose cache is actually
/// wrong needs to hear about it. Reuses the platform's existing
/// site_id-partitioned topic + idempotent event_id delivery, exactly like
/// manifest.created/manifest.updated (D032).
/// </summary>
public sealed record EpcReturnedEvent(
    string EventId, string Epc, string OriginatingSiteId,
    InspectionOutcome Outcome, DateTimeOffset ReturnedAt);

public interface IReturnEventPublisher
{
    /// <summary>
    /// FraudHold is deliberately NEVER published here -- ownership is
    /// disputed, so the originating store's cache must keep alarming on
    /// this EPC via EAS until Loss Prevention resolves the case. Publishing
    /// on FraudHold would silently reopen the exact gap this decision exists
    /// to close.
    /// </summary>
    void PublishEpcReturned(EpcReturnedEvent evt);
}

/// <summary>
/// Thrown when RecordInspection or AuthorizeRefund is called out of order --
/// makes the saga's step sequence a code-enforced invariant, the same way
/// GateSession.Close() enforces zero-loss: illegal transitions throw rather
/// than silently producing an inconsistent result.
/// </summary>
public sealed class ReturnSagaStateException : Exception
{
    public ReturnSagaStateException(string message) : base(message) { }
}

/// <summary>
/// One instance per return attempt. Owns the multi-step
/// verify -> inspect -> refund-authorize/compensate sequence (Saga Pattern,
/// D034) -- something has to see and gate the WHOLE sequence, the same
/// reasoning that won Saga over pure choreography in D031 (PTL). Unlike
/// GateSession (P027/D032), which enforces a single point-in-time invariant
/// against a pre-positioned manifest, ReturnSaga spans multiple real-world
/// steps with an actual compensating action (deny the refund, quarantine
/// the item) if verification fails.
/// </summary>
public sealed class ReturnSaga
{
    private readonly string _epc;
    private readonly string? _tid;
    private readonly ReturnLocality _locality;
    private readonly IPaidEpcCache _localPaidEpcCache;
    private readonly ILocalTidBindingCache _localTidCache;
    private readonly ICentralReturnVerifier _centralVerifier;
    private readonly ISoldEpcLedger? _countOnlyLedger; // null for Serialized GTINs
    private readonly bool _isCountOnlyGtin;
    private readonly TimeSpan _crossStoreVerificationTimeout;

    private ReturnVerdict? _verdict;
    private InspectionOutcome? _inspection;
    private string? _originatingSiteId;

    public string SessionId { get; }

    public ReturnSaga(
        string sessionId, string epc, string? tid, ReturnLocality locality,
        bool isCountOnlyGtin, IPaidEpcCache localPaidEpcCache,
        ILocalTidBindingCache localTidCache, ICentralReturnVerifier centralVerifier,
        ISoldEpcLedger? countOnlyLedger, TimeSpan crossStoreVerificationTimeout)
    {
        SessionId = sessionId;
        _epc = epc;
        _tid = tid;
        _locality = locality;
        _isCountOnlyGtin = isCountOnlyGtin;
        _localPaidEpcCache = localPaidEpcCache;
        _localTidCache = localTidCache;
        _centralVerifier = centralVerifier;
        _countOnlyLedger = countOnlyLedger;
        _crossStoreVerificationTimeout = crossStoreVerificationTimeout;
    }

    /// <summary>
    /// D034 core branch: locality decides how the verdict is reached, not a
    /// single unconditional path. SameStore never touches the network --
    /// local cache presence IS the proof. CrossStore is the platform's one
    /// deliberate, scoped exception to "no synchronous registry calls from
    /// site operations" -- bounded by an explicit timeout, and a timeout
    /// resolves to PendingVerification, never a silent pass or a blind deny.
    /// </summary>
    public async Task<ReturnVerdict> VerifyAsync(CancellationToken ct)
    {
        if (_verdict is not null)
            throw new ReturnSagaStateException($"ReturnSaga {SessionId} already verified.");

        if (_locality == ReturnLocality.SameStore)
        {
            _verdict = VerifySameStore();
            return _verdict.Value;
        }

        _verdict = await VerifyCrossStoreAsync(ct);
        return _verdict.Value;
    }

    private ReturnVerdict VerifySameStore()
    {
        // Local cache presence is sufficient local proof for SameStore --
        // zero network, matching checkout/EAS's existing offline guarantee.
        if (!_localPaidEpcCache.Contains(_epc))
            return ReturnVerdict.Rejected;

        if (RequiresTidCheck())
        {
            var bound = _localTidCache.TryGetBoundTid(_epc, out var localTid);
            if (!bound || localTid != _tid)
                return ReturnVerdict.Rejected; // forces FraudHold at inspection
        }

        _originatingSiteId = null; // caller already knows: it's this store
        return ReturnVerdict.Verified;
    }

    private async Task<ReturnVerdict> VerifyCrossStoreAsync(CancellationToken ct)
    {
        // D034: CountOnly GTINs have no epc_registry lifecycle to check
        // centrally either -- fall back to the short-lived sold-EPC ledger
        // instead of the normal central verifier.
        if (_isCountOnlyGtin)
        {
            var entry = _countOnlyLedger?.TryGet(_epc);
            if (entry is null)
                return ReturnVerdict.PendingVerification; // expired/unknown,
                                                            // not an outright deny
            _originatingSiteId = entry.SiteId;
            return ReturnVerdict.Verified;
        }

        CentralVerificationResult? result;
        try
        {
            result = await _centralVerifier.TryVerifyAsync(
                _epc, _tid, _crossStoreVerificationTimeout, ct);
        }
        catch (OperationCanceledException)
        {
            result = null; // timeout -- treated identically to unreachable
        }

        if (result is null)
            return ReturnVerdict.PendingVerification; // D034's third
                                                        // fail-safe outcome

        if (!result.SaleConfirmed || (RequiresTidCheck() && !result.TidBindingOk))
            return ReturnVerdict.Rejected;

        _originatingSiteId = result.OriginatingSiteId;
        return ReturnVerdict.Verified;
    }

    private bool RequiresTidCheck() => !_isCountOnlyGtin; // high-value,
        // Serialized-mode SKUs only -- CountOnly never gets a tid_registry
        // check, consistent with it being the low-value tier by design.

    /// <summary>
    /// The employee's condition check at the counter, unavoidable in any
    /// real return process -- recorded in the SAME transaction as the
    /// sold -> returned epc_registry transition, so Resellable items land
    /// straight in store_stock with no separate trigger (mirrors the
    /// existing encoded -> in_stock precedent for in-house tagging).
    /// A FraudHold verdict from VerifyAsync forces FraudHold here regardless
    /// of what the employee selects -- the saga's own verdict cannot be
    /// downgraded by inspection, only escalated.
    /// </summary>
    public ReturnSagaResult RecordInspection(InspectionOutcome employeeOutcome)
    {
        if (_verdict is null)
            throw new ReturnSagaStateException(
                $"ReturnSaga {SessionId} cannot record inspection before VerifyAsync.");
        if (_inspection is not null)
            throw new ReturnSagaStateException(
                $"ReturnSaga {SessionId} inspection already recorded.");

        _inspection = _verdict == ReturnVerdict.Rejected
            ? InspectionOutcome.FraudHold // compensating action: a Rejected
                                            // verdict always forces FraudHold,
                                            // never silently downgraded
            : employeeOutcome;

        return new ReturnSagaResult(SessionId, _epc, _verdict.Value, _inspection.Value,
            _originatingSiteId, RefundAuthorized: _verdict.Value != ReturnVerdict.Rejected
                && _verdict.Value != ReturnVerdict.PendingVerification);
    }

    /// <summary>
    /// D034: closes the saga and, for SameStore, prunes the local cache
    /// in-process immediately (mirrors the existing POS-bridge "mark sold ->
    /// update cache" pattern, in reverse). For CrossStore, publishes
    /// EpcReturnedEvent so the ORIGINATING store's cache gets pruned --
    /// never this store's, and never a broadcast to every store.
    /// FraudHold deliberately never triggers any cache removal at all --
    /// ownership is disputed, so EAS must keep alarming on this EPC until
    /// Loss Prevention resolves the case.
    /// </summary>
    public void Close(IReturnEventPublisher publisher, string thisSiteId, string eventId)
    {
        if (_inspection is null)
            throw new ReturnSagaStateException(
                $"ReturnSaga {SessionId} cannot close before RecordInspection.");

        if (_inspection == InspectionOutcome.FraudHold)
            return; // no cache mutation -- EAS must keep alarming, by design

        if (_locality == ReturnLocality.SameStore)
        {
            _localPaidEpcCache.Remove(_epc); // zero network, same as add-on-sale
            return;
        }

        var originating = _originatingSiteId
            ?? throw new ReturnSagaStateException(
                $"ReturnSaga {SessionId}: CrossStore Verified/PendingVerification " +
                "result missing OriginatingSiteId -- cannot route cache invalidation.");

        publisher.PublishEpcReturned(new EpcReturnedEvent(
            eventId, _epc, originating, _inspection.Value, DateTimeOffset.UtcNow));
    }
}

public sealed record ReturnSagaResult(
    string SessionId, string Epc, ReturnVerdict Verdict, InspectionOutcome Inspection,
    string? OriginatingSiteId, bool RefundAuthorized);

/// <summary>
/// D034: symmetric counterpart to the existing POS-bridge cache writer that
/// already consumes item.sold to ADD to the paid-EPC cache. Runs at the
/// ORIGINATING store's Store Gateway only -- delivered via the same
/// HTTPS/mTLS poll + site_id-partitioned Kafka pipeline every other
/// cross-site event in this platform already uses (D032 Addendum 2), never
/// a direct broker subscription across the WAN. Idempotent by construction:
/// removing an already-absent EPC is a no-op, so at-least-once redelivery
/// of the same event_id is always safe.
/// </summary>
public sealed class StoreGatewayReturnCacheInvalidator
{
    private readonly IPaidEpcCache _cache;

    public StoreGatewayReturnCacheInvalidator(IPaidEpcCache cache) => _cache = cache;

    public void OnEpcReturnedEvent(EpcReturnedEvent evt)
    {
        // FraudHold never reaches this consumer at all -- Close() above
        // never publishes for that outcome -- but the guard is kept here
        // too as a defense-in-depth invariant, not a trust boundary.
        if (evt.Outcome == InspectionOutcome.FraudHold)
            return;

        _cache.Remove(evt.Epc);
    }
}
