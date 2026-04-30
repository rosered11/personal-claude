using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Queries.ListOrders;

public class ListOrdersHandler(IOrderRepository orderRepository) : IRequestHandler<ListOrdersQuery, Result<PaginatedResult<OrderDto>>>
{
    public async Task<Result<PaginatedResult<OrderDto>>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await orderRepository.ListAsync(
            request.Status,
            request.StoreId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(order => new OrderDto(
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
        )).ToList();

        return Result.Ok(new PaginatedResult<OrderDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
