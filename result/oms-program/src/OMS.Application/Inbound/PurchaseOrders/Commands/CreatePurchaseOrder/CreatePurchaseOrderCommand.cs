using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Inbound.PurchaseOrders.Commands.CreatePurchaseOrder;

public record CreatePurchaseOrderLineDto(
    string Sku,
    string ProductName,
    decimal OrderedQty,
    UnitOfMeasure UnitOfMeasure);

public record CreatePurchaseOrderCommand(
    string PoNumber,
    string SupplierId,
    Guid StoreId,
    IReadOnlyList<CreatePurchaseOrderLineDto> Lines,
    string CreatedBy
) : IRequest<Result<Guid>>;
