using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Inbound.PurchaseOrders.Commands.ConfirmGoodsReceipt;

public record GoodsReceiptLineDto(Guid LineId, decimal ReceivedQty, ItemCondition Condition);

public record ConfirmGoodsReceiptCommand(
    Guid PurchaseOrderId,
    IReadOnlyList<GoodsReceiptLineDto> Lines,
    string UpdatedBy
) : IRequest<Result>;
