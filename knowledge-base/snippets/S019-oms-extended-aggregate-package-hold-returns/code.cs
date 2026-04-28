// ============================================================
// S019 — OMS Extended Order Aggregate
//        Package, OnHold, Returns, PackageLost, Multi-Channel
// Stack: .NET 8, MediatR, EF Core, PostgreSQL
// Extends: S018 (P013/D018 baseline)
// ============================================================

// ---- DOMAIN LAYER — ENUMS ----

/// <summary>
/// Full OMS order lifecycle including Phase 2 extensions.
/// Extension states: Packed, OnHold, ReturnRequested, ReturnPickupScheduled, Returned, PaymentFailed.
/// </summary>
public enum OrderStatus
{
    // --- Phase 1 states (D018) ---
    Pending,
    BookingConfirmed,
    PickStarted,
    PickConfirmed,
    OutForDelivery,
    Delivered,
    Invoiced,
    Paid,           // terminal
    Cancelled,      // terminal

    // --- Phase 2 extensions (D019) ---
    Packed,                  // WMS called AssignPackages; physical packing complete
    OnHold,                  // non-destructive pause; PreHoldState stored
    ReturnRequested,         // customer initiated return (from Delivered only)
    ReturnPickupScheduled,   // TMS return pickup booked
    Returned,                // terminal — return confirmed
    PaymentFailed            // terminal — invoice payment rejected
}

/// <summary>
/// Extended order channel types for multi-channel retail.
/// </summary>
public enum ChannelType
{
    Gateway,      // Phase 1 — standard online channel
    B2B,          // Phase 1 — business-to-business
    Kiosk,        // Phase 1 — in-store kiosk
    Marketplace,  // Phase 2 — Shopee, Lazada, TikTok Shop
    POSTerminal,  // Phase 2 — in-store POS terminal
    BulkImport    // Phase 2 — batch file import
}

public enum PackageStatus { Packed, OutForDelivery, Delivered, Lost }

// ---- DOMAIN LAYER — VALUE OBJECTS ----

/// <summary>
/// Package value object — replaces Shipment entity.
/// TMS reports delivery events per TrackingId (physical box), not per vehicle group.
/// Owned by the Order aggregate; multiple packages per order are supported.
/// </summary>
public record Package(
    Guid PackageId,
    string TrackingId,
    string VehicleType,
    PackageStatus Status,
    IReadOnlyList<Guid> OrderLineIds
);

// ---- DOMAIN LAYER — DOMAIN EVENTS ----

public interface IDomainEvent { }

// Phase 1 events (D018) — preserved
public record OrderCreated(Guid OrderId, string StoreCode, ChannelType Channel) : IDomainEvent;
public record BookingConfirmed(Guid OrderId, string BookingRef) : IDomainEvent;
public record PickStarted(Guid OrderId, string PickerId) : IDomainEvent;
public record PickConfirmed(Guid OrderId) : IDomainEvent;
public record OutForDelivery(Guid OrderId) : IDomainEvent;
public record OrderDelivered(Guid OrderId, DateTimeOffset DeliveredAt) : IDomainEvent;
public record OrderCancelled(Guid OrderId, string Reason) : IDomainEvent;

// Phase 2 events (D019) — new
public record PackagesAssigned(Guid OrderId, IReadOnlyList<Package> Packages) : IDomainEvent;
public record OrderPlacedOnHold(Guid OrderId, string Reason, OrderStatus PreHoldState) : IDomainEvent;
public record OrderReleased(Guid OrderId, OrderStatus RestoredState) : IDomainEvent;
public record ReturnRequested(Guid OrderId, string Reason) : IDomainEvent;
public record ReturnPickupScheduled(Guid OrderId, string ReturnTrackingId) : IDomainEvent;
public record OrderReturned(Guid OrderId, DateTimeOffset ReturnedAt) : IDomainEvent;
public record PackageLostReported(Guid OrderId, string LostTrackingId) : IDomainEvent;
public record PaymentFailed(Guid OrderId, string InvoiceRef, string FailureReason) : IDomainEvent;

// ---- DOMAIN LAYER — ORDER AGGREGATE ----

/// <summary>
/// Order aggregate root. Owns the full OMS state machine including Phase 2 extensions.
///
/// Invariants enforced:
/// - All state transitions via aggregate methods; no external Status mutation.
/// - OnHold stores PreHoldState and restores on Release.
/// - AssignPackages only from PickConfirmed.
/// - RequestReturn only from Delivered.
/// - PlaceOnHold blocked from all terminal states.
/// - PackageLost auto-triggers OnHold via PlaceOnHold.
/// </summary>
public class Order
{
    // ---- Identity & Core ----
    public Guid Id { get; private set; }
    public string StoreCode { get; private set; } = default!;
    public ChannelType Channel { get; private set; }
    public OrderStatus Status { get; private set; }

    // ---- Phase 2: OnHold ----
    private OrderStatus? _preHoldState;
    public OrderStatus? PreHoldState => _preHoldState; // exposed for persistence only

    // ---- Phase 2: Package collection ----
    private List<Package> _packages = new();
    public IReadOnlyList<Package> Packages => _packages.AsReadOnly();

    // ---- Domain event queue ----
    private readonly List<IDomainEvent> _events = new();

    // EF Core materialization constructor
    private Order() { }

    // ---- Factory ----

    public static Order Create(Guid id, string storeCode, ChannelType channel = ChannelType.Gateway)
    {
        var o = new Order
        {
            Id = id,
            StoreCode = storeCode,
            Channel = channel,
            Status = OrderStatus.Pending
        };
        o._events.Add(new OrderCreated(id, storeCode, channel));
        return o;
    }

    // ---- Phase 1 transitions (D018 — unchanged guards) ----

    public void ConfirmBooking(string bookingRef)
    {
        Guard(OrderStatus.Pending, nameof(ConfirmBooking));
        Status = OrderStatus.BookingConfirmed;
        _events.Add(new BookingConfirmed(Id, bookingRef));
    }

    public void StartPick(string pickerId)
    {
        Guard(OrderStatus.BookingConfirmed, nameof(StartPick));
        Status = OrderStatus.PickStarted;
        _events.Add(new PickStarted(Id, pickerId));
    }

    public void ConfirmPick()
    {
        Guard(OrderStatus.PickStarted, nameof(ConfirmPick));
        Status = OrderStatus.PickConfirmed;
        _events.Add(new PickConfirmed(Id));
    }

    public void MarkOutForDelivery()
    {
        // Phase 2: OutForDelivery now requires Packed (not PickConfirmed)
        Guard(OrderStatus.Packed, nameof(MarkOutForDelivery));
        Status = OrderStatus.OutForDelivery;
        _events.Add(new OutForDelivery(Id));
    }

    public void MarkDelivered(DateTimeOffset deliveredAt)
    {
        Guard(OrderStatus.OutForDelivery, nameof(MarkDelivered));
        Status = OrderStatus.Delivered;
        _events.Add(new OrderDelivered(Id, deliveredAt));
    }

    public void GenerateInvoice()
    {
        Guard(OrderStatus.Delivered, nameof(GenerateInvoice));
        Status = OrderStatus.Invoiced;
        // domain event omitted for brevity — follow D018 pattern
    }

    public void NotifyPaid()
    {
        Guard(OrderStatus.Invoiced, nameof(NotifyPaid));
        Status = OrderStatus.Paid;
        // domain event omitted for brevity — follow D018 pattern
    }

    public void Cancel(string reason)
    {
        if (IsTerminal())
            throw new InvalidOperationException($"Cannot cancel an order in terminal state '{Status}'.");
        Status = OrderStatus.Cancelled;
        _events.Add(new OrderCancelled(Id, reason));
    }

    // ---- Phase 2 transitions (D019) ----

    /// <summary>
    /// WMS calls this after physical packing is complete.
    /// Assigns packages (with TrackingIds) and advances order to Packed.
    /// Packed is required before MarkOutForDelivery.
    /// </summary>
    public void AssignPackages(IEnumerable<Package> packages)
    {
        Guard(OrderStatus.PickConfirmed, nameof(AssignPackages));
        var packageList = packages?.ToList()
            ?? throw new ArgumentNullException(nameof(packages));
        if (packageList.Count == 0)
            throw new InvalidOperationException("At least one package is required.");

        _packages.AddRange(packageList);
        Status = OrderStatus.Packed;
        _events.Add(new PackagesAssigned(Id, _packages.AsReadOnly()));
    }

    /// <summary>
    /// Non-destructive pause. Stores current state in _preHoldState for resume.
    /// Blocked from all terminal states (Delivered, Paid, Cancelled, Returned, PaymentFailed).
    /// </summary>
    public void PlaceOnHold(string reason)
    {
        if (Status == OrderStatus.OnHold)
            throw new InvalidOperationException("Order is already on hold.");
        if (IsTerminal())
            throw new InvalidOperationException($"Cannot hold an order in terminal state '{Status}'.");

        _preHoldState = Status;
        Status = OrderStatus.OnHold;
        _events.Add(new OrderPlacedOnHold(Id, reason, _preHoldState.Value));
    }

    /// <summary>
    /// Resumes order from OnHold to the exact pre-hold state.
    /// Idempotent: if already released, throws (prevents double-release).
    /// </summary>
    public void Release()
    {
        Guard(OrderStatus.OnHold, nameof(Release));
        var restoredState = _preHoldState
            ?? throw new InvalidOperationException("No pre-hold state recorded. Data integrity error.");

        Status = restoredState;
        _preHoldState = null;
        _events.Add(new OrderReleased(Id, restoredState));
    }

    /// <summary>
    /// Reports a package as lost. Marks Package.Status = Lost and auto-triggers OnHold.
    /// Staff resolves via Release() + re-dispatch or Cancel().
    /// </summary>
    public void ReportPackageLost(string trackingId)
    {
        var pkg = _packages.FirstOrDefault(p => p.TrackingId == trackingId)
            ?? throw new InvalidOperationException($"Package with TrackingId '{trackingId}' not found on this order.");

        if (pkg.Status == PackageStatus.Lost)
            return; // idempotent — already marked lost

        _packages = _packages
            .Select(p => p.TrackingId == trackingId ? p with { Status = PackageStatus.Lost } : p)
            .ToList();

        _events.Add(new PackageLostReported(Id, trackingId));

        // Auto-trigger OnHold for staff resolution
        PlaceOnHold($"PackageLost:{trackingId}");
    }

    /// <summary>
    /// Customer initiates a return. Only valid from Delivered state.
    /// Raises ReturnRequested event — SprintConnectFulfillmentAdapter calls TMS return pickup API.
    /// </summary>
    public void RequestReturn(string reason)
    {
        Guard(OrderStatus.Delivered, nameof(RequestReturn));
        Status = OrderStatus.ReturnRequested;
        _events.Add(new ReturnRequested(Id, reason));
    }

    /// <summary>
    /// TMS has scheduled return pickup. ACL maps TMS callback to this command.
    /// </summary>
    public void ScheduleReturnPickup(string returnTrackingId)
    {
        Guard(OrderStatus.ReturnRequested, nameof(ScheduleReturnPickup));
        if (string.IsNullOrWhiteSpace(returnTrackingId))
            throw new ArgumentException("Return tracking ID is required.", nameof(returnTrackingId));

        Status = OrderStatus.ReturnPickupScheduled;
        _events.Add(new ReturnPickupScheduled(Id, returnTrackingId));
    }

    /// <summary>
    /// TMS confirms return delivery. Terminal state.
    /// </summary>
    public void ConfirmReturn()
    {
        Guard(OrderStatus.ReturnPickupScheduled, nameof(ConfirmReturn));
        Status = OrderStatus.Returned;
        _events.Add(new OrderReturned(Id, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Invoice payment was rejected. Terminal state requiring manual intervention.
    /// </summary>
    public void ReportPaymentFailure(string invoiceRef, string failureReason)
    {
        Guard(OrderStatus.Invoiced, nameof(ReportPaymentFailure));
        Status = OrderStatus.PaymentFailed;
        _events.Add(new PaymentFailed(Id, invoiceRef, failureReason));
    }

    // ---- Event management ----

    /// <summary>
    /// Drains all queued domain events and clears the internal list.
    /// Call after SaveAsync to dispatch events to the outbox.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DrainEvents()
    {
        var copy = _events.ToList();
        _events.Clear();
        return copy;
    }

    // ---- Private helpers ----

    private void Guard(OrderStatus required, string operation)
    {
        if (Status != required)
            throw new InvalidOperationException(
                $"{operation} requires status '{required}', but current status is '{Status}'.");
    }

    private bool IsTerminal() =>
        Status is OrderStatus.Delivered
                or OrderStatus.Paid
                or OrderStatus.Cancelled
                or OrderStatus.Returned
                or OrderStatus.PaymentFailed;
}

// ---- DOMAIN LAYER — CHANNEL FACTORY ----

/// <summary>
/// Channel-specific order creation validation.
/// BulkImport bypasses RolloutPolicy — pre-authorized batch flow.
/// </summary>
public static class ChannelOrderFactory
{
    public static Order CreateForChannel(
        Guid orderId,
        string storeCode,
        ChannelType channel,
        IDictionary<string, string>? channelMetadata = null)
    {
        ValidateChannelMetadata(channel, channelMetadata);
        return Order.Create(orderId, storeCode, channel);
    }

    private static void ValidateChannelMetadata(ChannelType channel, IDictionary<string, string>? meta)
    {
        meta ??= new Dictionary<string, string>();
        switch (channel)
        {
            case ChannelType.Marketplace:
                Require(meta, "MarketplaceOrderRef", channel);
                Require(meta, "MarketplaceName", channel);
                break;
            case ChannelType.POSTerminal:
                Require(meta, "TerminalId", channel);
                Require(meta, "StoreCode", channel);
                break;
            case ChannelType.BulkImport:
                Require(meta, "BatchFileRef", channel);
                break;
            // Gateway, B2B, Kiosk: no additional metadata required
        }
    }

    private static void Require(IDictionary<string, string> meta, string key, ChannelType channel)
    {
        if (!meta.ContainsKey(key) || string.IsNullOrWhiteSpace(meta[key]))
            throw new InvalidOperationException(
                $"Channel '{channel}' requires metadata key '{key}'.");
    }
}

// ---- APPLICATION LAYER — SAMPLE COMMAND HANDLER ----

/// <summary>
/// AssignPackagesHandler — invoked when WMS calls back after physical packing.
/// Follows D018 CreateOrderHandler pattern: idempotency check + aggregate method + outbox.
/// </summary>
public record AssignPackagesCommand(
    Guid OrderId,
    IReadOnlyList<Package> Packages
) : IRequest<Result>;

public class AssignPackagesHandler : IRequestHandler<AssignPackagesCommand, Result>
{
    private readonly IOrderRepository _repo;
    private readonly IOutboxWriter _outbox;

    public AssignPackagesHandler(IOrderRepository repo, IOutboxWriter outbox)
    {
        _repo = repo;
        _outbox = outbox;
    }

    public async Task<Result> Handle(AssignPackagesCommand cmd, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(cmd.OrderId, ct);
        if (order is null)
            return Result.Fail($"Order '{cmd.OrderId}' not found.");

        // Idempotency: if already Packed, packages were already assigned — safe to return Ok
        if (order.Status == OrderStatus.Packed)
            return Result.Ok();

        order.AssignPackages(cmd.Packages);

        await using var tx = await _repo.BeginTransactionAsync(ct);
        await _repo.SaveAsync(order, ct);

        foreach (var evt in order.DrainEvents())
            await _outbox.WriteAsync(evt, ct);

        await tx.CommitAsync(ct);
        return Result.Ok();
    }
}

// ---- APPLICATION LAYER — HOLD/RELEASE HANDLERS ----

public record PlaceOnHoldCommand(Guid OrderId, string Reason) : IRequest<Result>;

public class PlaceOnHoldHandler : IRequestHandler<PlaceOnHoldCommand, Result>
{
    private readonly IOrderRepository _repo;
    private readonly IOutboxWriter _outbox;

    public PlaceOnHoldHandler(IOrderRepository repo, IOutboxWriter outbox)
    {
        _repo = repo;
        _outbox = outbox;
    }

    public async Task<Result> Handle(PlaceOnHoldCommand cmd, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(cmd.OrderId, ct);
        if (order is null) return Result.Fail($"Order '{cmd.OrderId}' not found.");

        // Idempotency: already on hold
        if (order.Status == OrderStatus.OnHold) return Result.Ok();

        order.PlaceOnHold(cmd.Reason);

        await using var tx = await _repo.BeginTransactionAsync(ct);
        await _repo.SaveAsync(order, ct);
        foreach (var evt in order.DrainEvents()) await _outbox.WriteAsync(evt, ct);
        await tx.CommitAsync(ct);
        return Result.Ok();
    }
}

public record ReleaseOrderCommand(Guid OrderId) : IRequest<Result>;

public class ReleaseOrderHandler : IRequestHandler<ReleaseOrderCommand, Result>
{
    private readonly IOrderRepository _repo;
    private readonly IOutboxWriter _outbox;

    public ReleaseOrderHandler(IOrderRepository repo, IOutboxWriter outbox)
    {
        _repo = repo;
        _outbox = outbox;
    }

    public async Task<Result> Handle(ReleaseOrderCommand cmd, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(cmd.OrderId, ct);
        if (order is null) return Result.Fail($"Order '{cmd.OrderId}' not found.");

        // Idempotency: not on hold — already released or never held
        if (order.Status != OrderStatus.OnHold) return Result.Ok();

        order.Release();

        await using var tx = await _repo.BeginTransactionAsync(ct);
        await _repo.SaveAsync(order, ct);
        foreach (var evt in order.DrainEvents()) await _outbox.WriteAsync(evt, ct);
        await tx.CommitAsync(ct);
        return Result.Ok();
    }
}

// ---- POSTGRESQL SCHEMA EXTENSIONS ----
/*
-- Extend orders table for Phase 2
ALTER TABLE orders
    ADD COLUMN pre_hold_state  VARCHAR(30)  NULL,       -- stored when Status = OnHold
    ADD COLUMN channel_type    VARCHAR(30)  NOT NULL DEFAULT 'Gateway',
    ADD COLUMN packages        JSONB        NULL;        -- Package[] as JSONB array

-- New order_events (outbox) entries for Phase 2 event types:
--   PackagesAssigned, OrderPlacedOnHold, OrderReleased
--   ReturnRequested, ReturnPickupScheduled, OrderReturned
--   PackageLostReported, PaymentFailed
-- No schema change needed — all events use existing order_events(event_type, payload JSONB) table.

-- Optional: normalized package table for SQL-level package queries
-- (e.g., "find all orders with lost packages")
CREATE TABLE order_packages (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id        UUID         NOT NULL REFERENCES orders(id),
    tracking_id     VARCHAR(100) NOT NULL,
    vehicle_type    VARCHAR(50)  NOT NULL,
    package_status  VARCHAR(20)  NOT NULL DEFAULT 'Packed',
    order_line_ids  JSONB        NOT NULL,  -- Guid[]
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    UNIQUE (order_id, tracking_id)
);

CREATE INDEX idx_order_packages_tracking ON order_packages(tracking_id);
CREATE INDEX idx_order_packages_status   ON order_packages(package_status)
    WHERE package_status IN ('Lost', 'OutForDelivery');
*/
