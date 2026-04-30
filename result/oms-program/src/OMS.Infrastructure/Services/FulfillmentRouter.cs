using OMS.Application.Common.Interfaces;
using OMS.Domain.Enums;

namespace OMS.Infrastructure.Services;

public class FulfillmentRouter : IFulfillmentRouter
{
    public bool RequiresBooking(FulfillmentType fulfillmentType) =>
        fulfillmentType == FulfillmentType.Delivery;

    public bool RequiresTms(FulfillmentType fulfillmentType) =>
        fulfillmentType == FulfillmentType.Delivery || fulfillmentType == FulfillmentType.Express;

    public OrderStatus GetInitialPickStatus(FulfillmentType fulfillmentType) =>
        fulfillmentType switch
        {
            FulfillmentType.Delivery => OrderStatus.BookingConfirmed,
            FulfillmentType.ClickAndCollect => OrderStatus.Pending,
            FulfillmentType.Express => OrderStatus.Pending,
            _ => OrderStatus.Pending
        };
}
