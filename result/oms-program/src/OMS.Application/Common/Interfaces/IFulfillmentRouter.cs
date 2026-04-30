using OMS.Domain.Enums;

namespace OMS.Application.Common.Interfaces;

public interface IFulfillmentRouter
{
    bool RequiresBooking(FulfillmentType fulfillmentType);
    bool RequiresTms(FulfillmentType fulfillmentType);
    OrderStatus GetInitialPickStatus(FulfillmentType fulfillmentType);
}
