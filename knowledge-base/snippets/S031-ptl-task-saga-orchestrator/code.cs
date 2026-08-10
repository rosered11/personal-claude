// PtlTaskSaga.cs
// D031: PTL Task Saga -- Orchestrated Process Manager over an Event-Carried State Backbone.
// Owns the allocation -> task -> confirm -> SO/STO -> remaining-sync lifecycle for one
// Put-to-Light box/task, enforcing P026's hard invariants and exception paths.
// No MediatR, no AutoMapper -- plain constructor DI and explicit manual mapping,
// per this repository's .NET coding standards.

namespace Cmg.Wms.PutToLight.Orchestration;

public enum PtlTaskState
{
    AllocationImported,
    TaskGenerated,
    SentToPtl,
    Confirmed,
    SoStoRequested,
    SoStoCreated,
    RemainingSynced,
    Completed,
    OnHold,     // allocation-vs-stock mismatch -- awaiting manual review
    Rejected    // mixed-store carton or other hard-invariant violation
}

public sealed class PtlTask
{
    public required string TaskId { get; init; }
    public required string OrderId { get; init; }   // 1 order = 1 box = 1 invoice
    public required string BoxId { get; init; }
    public required string PltSlotId { get; init; }
    public required string StoreId { get; init; }
    public PtlTaskState State { get; private set; } = PtlTaskState.AllocationImported;
    public List<PtlLineItem> Lines { get; } = new();
    public string? HoldOrRejectReason { get; private set; }

    public void Transition(PtlTaskState next, string? reason = null)
    {
        // Centralized guard: every state change for this task passes through here,
        // giving warehouse ops one auditable place to trace task lifecycle --
        // replacing today's manual Excel-based tracing (P026 constraint).
        State = next;
        if (next is PtlTaskState.OnHold or PtlTaskState.Rejected)
            HoldOrRejectReason = reason;
    }
}

public sealed record PtlLineItem(string Lpn, string Sku, string StoreId, int AllocatedQty, int ConfirmedQty);

public interface IWmsClient
{
    Task<int> GetStockQtyAsync(string lpn, string sku, CancellationToken ct);
}

public interface IPtlControllerClient
{
    Task SendTaskAsync(PtlTask task, CancellationToken ct);
}

public interface ISapClient
{
    // Idempotency key prevents duplicate SO/STO creation on saga-step retry.
    Task<string> CreatePartialSoStoAsync(string orderId, IReadOnlyList<PtlLineItem> readyLines, string idempotencyKey, CancellationToken ct);
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct);
}

public sealed record PtlTaskConfirmed(string TaskId, IReadOnlyList<PtlLineItem> ConfirmedLines);
public sealed record SoStoCreated(string OrderId, string SoStoId, IReadOnlyList<string> Lpns);
public sealed record TaskRejected(string TaskId, string Reason);

public sealed class PtlTaskSaga
{
    private readonly IWmsClient _wms;
    private readonly IPtlControllerClient _ptl;
    private readonly ISapClient _sap;
    private readonly IEventBus _events;

    public PtlTaskSaga(IWmsClient wms, IPtlControllerClient ptl, ISapClient sap, IEventBus events)
    {
        _wms = wms;
        _ptl = ptl;
        _sap = sap;
        _events = events;
    }

    /// <summary>
    /// Hardware-controller confirmation callback. Rejects mixed-store cartons
    /// synchronously -- the reason a pure Event-Driven listener could not satisfy this
    /// constraint: the PTL controller needs an immediate error, not an eventually
    /// consistent reaction to a published event.
    /// </summary>
    public async Task<bool> ConfirmFromPtlController(PtlTask task, IReadOnlyList<PtlLineItem> confirmedLines, CancellationToken ct)
    {
        var distinctStores = confirmedLines.Select(l => l.StoreId).Distinct().Count();
        if (distinctStores > 1)
        {
            task.Transition(PtlTaskState.Rejected, "mixed-store carton");
            await _events.PublishAsync(new TaskRejected(task.TaskId, "mixed-store carton"), ct);
            return false; // synchronous rejection back to the controller
        }

        task.Transition(PtlTaskState.Confirmed);
        await _events.PublishAsync(new PtlTaskConfirmed(task.TaskId, confirmedLines), ct);
        return true;
    }

    /// <summary>
    /// Explicit two-directional allocation-vs-stock mismatch handling (P026 constraint):
    /// puts the task OnHold for review rather than silently proceeding or failing.
    /// </summary>
    public async Task EvaluateAllocationVsStock(PtlTask task, CancellationToken ct)
    {
        foreach (var line in task.Lines)
        {
            var actualStock = await _wms.GetStockQtyAsync(line.Lpn, line.Sku, ct);
            if (actualStock != line.AllocatedQty)
            {
                var direction = actualStock > line.AllocatedQty ? "stock > allocation" : "stock < allocation";
                task.Transition(PtlTaskState.OnHold, $"allocation mismatch: {direction} for {line.Lpn}/{line.Sku}");
                return;
            }
        }
        task.Transition(PtlTaskState.TaskGenerated);
    }

    /// <summary>
    /// Partial SO/STO creation: a task can request SO/STO independently of sibling
    /// tasks under the same order/allocation, satisfying P026's partial-fulfillment
    /// requirement without waiting for full order completion.
    /// </summary>
    public async Task TryRequestPartialSoSto(PtlTask task, CancellationToken ct)
    {
        if (task.State != PtlTaskState.Confirmed) return;

        task.Transition(PtlTaskState.SoStoRequested);
        var idempotencyKey = $"{task.OrderId}:{task.TaskId}"; // guards against duplicate SO/STO on retry
        var soStoId = await _sap.CreatePartialSoStoAsync(task.OrderId, task.Lines, idempotencyKey, ct);

        task.Transition(PtlTaskState.SoStoCreated);
        await _events.PublishAsync(new SoStoCreated(task.OrderId, soStoId, task.Lines.Select(l => l.Lpn).ToList()), ct);
    }
}
