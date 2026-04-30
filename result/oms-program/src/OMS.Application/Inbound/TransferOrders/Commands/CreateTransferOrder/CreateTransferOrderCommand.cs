using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Inbound.TransferOrders.Commands.CreateTransferOrder;

public record CreateTransferOrderLineDto(
    string Sku,
    string ProductName,
    decimal RequestedQty,
    UnitOfMeasure UnitOfMeasure);

public record CreateTransferOrderCommand(
    string TransferNumber,
    Guid SourceStoreId,
    Guid DestStoreId,
    IReadOnlyList<CreateTransferOrderLineDto> Lines,
    string CreatedBy
) : IRequest<Result<Guid>>;
