// ============================================================
// S018 — OMS: DDD Order Aggregate + CQRS CreateOrderHandler
// Stack: .NET 8, MediatR, EF Core, PostgreSQL
// ============================================================

// ---- DOMAIN LAYER ----

public enum OrderStatus
{
    Pending,
    BookingConfirmed,
    PickStarted,
    PickConfirmed,
    OutForDelivery,
    Delivered,
    Invoiced,
    Paid,
    Cancelled
}

public interface IDomainEvent { }

public record OrderCreated(Guid OrderId, string StoreCode) : IDomainEvent;
public record BookingConfirmed(Guid OrderId, string BookingRef) : IDomainEvent;
public record PickStarted(Guid OrderId, string PickerId) : IDomainEvent;
public record PickConfirmed(Guid OrderId) : IDomainEvent;
public record OutForDelivery(Guid OrderId) : IDomainEvent;
public record OrderDelivered(Guid OrderId, DateTimeOffset DeliveredAt) : IDomainEvent;
public record OrderCancelled(Guid OrderId, string Reason) : IDomainEvent;

/// <summary>
/// Order aggregate root. Owns the order state machine.
/// All state transitions are enforced here — no external code mutates Status directly.
/// </summary>
public class Order
{
    public Guid Id { get; private set; }
    public string StoreCode { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    private readonly List<IDomainEvent> _events = new();

    // EF Core needs a private constructor for materialization
    private Order() { }

    public static Order Create(Guid id, string storeCode)
    {
        var o = new Order
        {
            Id = id,
            StoreCode = storeCode,
            Status = OrderStatus.Pending
        };
        o._events.Add(new OrderCreated(id, storeCode));
        return o;
    }

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
        Guard(OrderStatus.PickConfirmed, nameof(MarkOutForDelivery));
        Status = OrderStatus.OutForDelivery;
        _events.Add(new OutForDelivery(Id));
    }

    public void MarkDelivered(DateTimeOffset deliveredAt)
    {
        Guard(OrderStatus.OutForDelivery, nameof(MarkDelivered));
        Status = OrderStatus.Delivered;
        _events.Add(new OrderDelivered(Id, deliveredAt));
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Paid)
            throw new InvalidOperationException("Cannot cancel a paid order.");
        Status = OrderStatus.Cancelled;
        _events.Add(new OrderCancelled(Id, reason));
    }

    /// <summary>
    /// Returns all queued domain events and clears the internal list.
    /// Call after SaveAsync to dispatch events to the outbox.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DrainEvents()
    {
        var copy = _events.ToList();
        _events.Clear();
        return copy;
    }

    private void Guard(OrderStatus required, string operation)
    {
        if (Status != required)
            throw new InvalidOperationException(
                $"{operation} requires status '{required}', but current status is '{Status}'.");
    }
}

// ---- DOMAIN SERVICES ----

/// <summary>
/// Gates commands to stores that have been rolled out in Phase 1.
/// Backed by a config table — no redeployment needed to enable a store.
/// </summary>
public class RolloutPolicy
{
    private readonly ISet<string> _enabledStores;

    public RolloutPolicy(IEnumerable<string> enabledStoreCodes)
        => _enabledStores = new HashSet<string>(enabledStoreCodes, StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled(string storeCode)
        => _enabledStores.Contains(storeCode);
}

// ---- APPLICATION LAYER — CQRS ----

public record CreateOrderCommand(Guid OrderId, string StoreCode) : IRequest<Result>;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Result>
{
    private readonly IOrderRepository _repo;
    private readonly IOutboxWriter _outbox;
    private readonly RolloutPolicy _rollout;

    public CreateOrderHandler(
        IOrderRepository repo,
        IOutboxWriter outbox,
        RolloutPolicy rollout)
    {
        _repo = repo;
        _outbox = outbox;
        _rollout = rollout;
    }

    public async Task<Result> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        // Phased rollout gate — stores not yet enabled return an error (not an exception)
        if (!_rollout.IsEnabled(cmd.StoreCode))
            return Result.Fail($"Store '{cmd.StoreCode}' is not yet on OMS rollout.");

        // Idempotency check — safe to retry (D015 pattern)
        if (await _repo.ExistsAsync(cmd.OrderId, ct))
            return Result.Ok(); // already created, return success

        var order = Order.Create(cmd.OrderId, cmd.StoreCode);

        await using var tx = await _repo.BeginTransactionAsync(ct);

        await _repo.SaveAsync(order, ct);

        // Write domain events to outbox — Sprint Connect adapter polls this table
        foreach (var evt in order.DrainEvents())
            await _outbox.WriteAsync(evt, ct);

        await tx.CommitAsync(ct);
        return Result.Ok();
    }
}

// ---- INFRASTRUCTURE INTERFACES (implement with EF Core + PostgreSQL) ----

public interface IOrderRepository
{
    Task<bool> ExistsAsync(Guid orderId, CancellationToken ct);
    Task SaveAsync(Order order, CancellationToken ct);
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
}

public interface IOutboxWriter
{
    Task WriteAsync(IDomainEvent evt, CancellationToken ct);
}

// ---- POSTGRESQL SCHEMA (run as migration) ----
/*
CREATE TABLE orders (
    id          UUID        PRIMARY KEY,
    store_code  VARCHAR(50) NOT NULL,
    status      VARCHAR(30) NOT NULL DEFAULT 'Pending',
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE order_lines (
    id          UUID        PRIMARY KEY,
    order_id    UUID        NOT NULL REFERENCES orders(id),
    sku         VARCHAR(100) NOT NULL,
    qty         INT         NOT NULL,
    basket_qty  INT         NULL  -- filled after PickConfirmed
);

-- Outbox table: Spring Connect adapter polls this
CREATE TABLE order_events (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id    UUID        NOT NULL REFERENCES orders(id),
    event_type  VARCHAR(100) NOT NULL,
    payload     JSONB       NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    processed_at TIMESTAMPTZ NULL   -- set by outbox poller after successful delivery
);

-- Read-side projection for POS and tracking queries
CREATE TABLE order_status_view (
    order_id        UUID        PRIMARY KEY,
    store_code      VARCHAR(50),
    status          VARCHAR(30),
    customer_name   VARCHAR(200),
    delivery_eta    TIMESTAMPTZ
);
*/
