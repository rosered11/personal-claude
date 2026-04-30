using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Inbound.PurchaseOrders.Commands.ConfirmPurchaseOrderPutAway;

public record ConfirmPurchaseOrderPutAwayCommand(
    Guid PurchaseOrderId,
    string UpdatedBy
) : IRequest<Result>;
