namespace OMS.Infrastructure.Outbox;

public static class OutboxEventTargetMapper
{
    private static readonly Dictionary<string, string[]> EventTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        // Inbound: order placed — WMS needs to know a new order exists for slot scheduling
        { "OrderCreatedEvent",               new[] { "WMS" } },

        // Delivery booking confirmed — WMS schedules the pick slot
        { "BookingConfirmedEvent",           new[] { "WMS" } },

        // OMS instructs WMS to start picking
        { "PickStartedEvent",                new[] { "WMS" } },

        // Pick done — POS needs quantities to recalculate pricing if partial
        { "PickConfirmedEvent",              new[] { "POS" } },

        // Recalculation applied — outbox record for timeline visibility (internal)
        { "PosRecalculationAppliedEvent",    Array.Empty<string>() },

        // Packages assigned / reassigned — TMS needs tracking + logistics details
        { "PackagesAssignedEvent",           new[] { "TMS" } },
        { "PackagesReassignedEvent",         new[] { "TMS" } },

        // Order packed — TMS can now schedule vehicle dispatch
        { "OrderPackedEvent",                new[] { "TMS" } },

        // Hold / release — pause or resume all active external operations
        { "OrderOnHoldEvent",                new[] { "WMS", "TMS" } },
        { "OrderReleasedEvent",              new[] { "WMS", "TMS" } },

        // Cancellation — notify every system that has an active task
        { "OrderCancelledEvent",             new[] { "WMS", "TMS", "POS" } },

        // Delivery reschedule — TMS updates its schedule
        { "OrderRescheduledEvent",           new[] { "TMS" } },

        // C&C: ready for customer collection — POS notifies customer
        { "ReadyForCollectionEvent",         new[] { "POS" } },

        // C&C: customer collected — POS triggers invoice generation
        { "OrderCollectedEvent",             new[] { "POS" } },

        // Delivery: all packages delivered — POS triggers invoice generation
        { "DeliveredEvent",                  new[] { "POS" } },

        // Invoice generated — POS receives invoice reference
        { "InvoiceGeneratedEvent",           new[] { "POS" } },

        // Payment confirmed — POS closes the transaction
        { "PaymentNotifiedEvent",            new[] { "POS" } },

        // Return: TMS schedules return pickup
        { "ReturnRequestedEvent",            new[] { "TMS" } },

        // Return: goods arrived at warehouse — internal event for timeline
        { "ReturnReceivedAtWarehouseEvent",  Array.Empty<string>() },

        // Put-away confirmed — triggers refund via POS
        { "PutAwayConfirmedEvent",           new[] { "POS" } },

        // Refund processed — POS issues credit note
        { "RefundProcessedEvent",            new[] { "POS" } },

        // Order marked Returned — internal, audit trail only
        { "OrderReturnedEvent",              Array.Empty<string>() },

        // Package lost / damaged — manual investigation; no automated outbound, hold covers WMS/TMS
        { "PackageLostEvent",                Array.Empty<string>() },
        { "PackageDamagedEvent",             Array.Empty<string>() },

        // Lines modified — WMS and POS need updated quantities
        { "OrderLinesModifiedEvent",         new[] { "WMS", "POS" } },

        // Substitution lifecycle — internal events for timeline visibility
        { "SubstitutionProposedEvent",       Array.Empty<string>() },
        { "SubstitutionApprovedEvent",       Array.Empty<string>() },
        { "SubstitutionRejectedEvent",       Array.Empty<string>() },

        // Inbound: PurchaseOrder lifecycle — WMS coordinates receiving and put-away
        { "PurchaseOrderCreatedEvent",           new[] { "WMS" } },
        { "GoodsReceiptConfirmedEvent",          Array.Empty<string>() },
        { "PurchaseOrderPutAwayConfirmedEvent",  Array.Empty<string>() },
        { "PurchaseOrderClosedEvent",            Array.Empty<string>() },

        // Inbound: TransferOrder lifecycle — WMS (source) picks, TMS ships, WMS (dest) receives
        { "TransferOrderCreatedEvent",           new[] { "WMS" } },
        { "TransferPickConfirmedEvent",          new[] { "TMS" } },
        { "TransferOrderInTransitEvent",         Array.Empty<string>() },
        { "TransferReceivedEvent",               Array.Empty<string>() },
        { "TransferOrderCompletedEvent",         Array.Empty<string>() },

        // Inbound: DamagedGoods receipt — internal events for audit trail
        { "DamagedGoodsReceivedEvent",           Array.Empty<string>() },
        { "DamagedGoodsPutAwayConfirmedEvent",   Array.Empty<string>() },
    };

    public static string[] GetTargets(string eventTypeName)
    {
        return EventTargets.TryGetValue(eventTypeName, out var targets)
            ? targets
            : Array.Empty<string>();
    }
}
