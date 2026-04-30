namespace OMS.Domain.ValueObjects;

public sealed record DeliverySlot(
    Guid SlotId,
    Guid StoreId,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd)
{
    public bool Overlaps(DeliverySlot other) =>
        ScheduledStart < other.ScheduledEnd && ScheduledEnd > other.ScheduledStart;
}
