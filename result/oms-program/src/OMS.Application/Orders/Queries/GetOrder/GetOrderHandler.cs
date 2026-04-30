using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Queries.GetOrder;

public class GetOrderHandler(IOrderRepository orderRepository) : IRequestHandler<GetOrderQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail<OrderDto>($"Order '{request.OrderId}' not found.");

        var dto = new OrderDto(
            order.OrderId,
            order.OrderNumber,
            order.SourceOrderId,
            order.ChannelType.ToString(),
            order.BusinessUnit,
            order.StoreId,
            order.OrderDate,
            order.Status.ToString(),
            order.PreHoldStatus?.ToString(),
            order.HoldReason,
            order.FulfillmentType.ToString(),
            order.PaymentMethod.ToString(),
            order.SubstitutionFlag,
            order.PosRecalcPending,
            order.CreatedAt,
            order.UpdatedAt,
            order.CreatedBy,
            order.UpdatedBy,
            order.OrderLines.Select(l => new OrderLineDto(
                l.OrderLineId,
                l.Sku,
                l.ProductName,
                l.Barcode,
                l.RequestedAmount,
                l.PickedAmount,
                l.UnitOfMeasure.ToString(),
                l.OriginalUnitPrice,
                l.Currency,
                l.Status.ToString(),
                l.IsSubstitute
            )).ToList(),
            order.Packages.Select(p => new OrderPackageDto(
                p.PackageId,
                p.TrackingId,
                p.CarrierPackageId,
                p.ThirdPartyLogistic,
                p.VehicleType.ToString(),
                p.Status.ToString(),
                p.PackageWeight,
                p.DeliveryNoteNumber,
                p.OrderLineIds
            )).ToList(),
            order.DeliverySlot is null ? null : new DeliverySlotDto(
                order.DeliverySlot.SlotId,
                order.DeliverySlot.StoreId,
                order.DeliverySlot.ScheduledStart,
                order.DeliverySlot.ScheduledEnd
            )
        );

        return Result.Ok(dto);
    }
}
