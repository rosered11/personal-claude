namespace OMS.Domain.Enums;

public enum OrderStatus
{
    Pending,
    BookingConfirmed,
    PickStarted,
    PickConfirmed,
    Packed,
    ReadyForCollection,
    Collected,
    OutForDelivery,
    Delivering,
    Delivered,
    Invoiced,
    Paid,
    OnHold,
    Cancelled,
    Returned,
    PaymentFailed
}
