using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;

namespace OMS.Application.Returns.Queries.GetReturn;

public class GetReturnHandler(IReturnRepository returnRepository) : IRequestHandler<GetReturnQuery, Result<ReturnDto>>
{
    public async Task<Result<ReturnDto>> Handle(GetReturnQuery request, CancellationToken cancellationToken)
    {
        var ret = await returnRepository.GetByIdAsync(request.ReturnId, cancellationToken);
        if (ret is null)
            return Result.Fail<ReturnDto>($"Return '{request.ReturnId}' not found.");

        var dto = new ReturnDto(
            ret.ReturnId,
            ret.OrderId,
            ret.ReturnOrderNumber,
            ret.InvoiceId,
            ret.CreditNoteId,
            ret.Status.ToString(),
            ret.GoodsReceiveNo,
            ret.ReturnReason,
            ret.RequestedAt,
            ret.PickupScheduledAt,
            ret.PickedUpAt,
            ret.ReceivedAt,
            ret.InspectedAt,
            ret.PutAwayAt,
            ret.RefundedAt,
            ret.CreatedAt,
            ret.UpdatedAt,
            ret.CreatedBy,
            ret.UpdatedBy,
            ret.ReturnItems.Select(i => new ReturnItemDto(
                i.ReturnItemId,
                i.OrderLineId,
                i.Sku,
                i.ProductName,
                i.Barcode,
                i.Quantity,
                i.UnitOfMeasure.ToString(),
                i.UnitPrice,
                i.Currency,
                i.ItemReason,
                i.Condition?.ToString(),
                i.PutAwayStatus,
                i.AssignedSloc,
                i.PaymentMethod.ToString()
            )).ToList()
        );

        return Result.Ok(dto);
    }
}
