using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Inbound.TransferOrders.Commands.ConfirmTransferPick;

public record TransferPickLineDto(Guid LineId, decimal TransferredQty);

public record ConfirmTransferPickCommand(
    Guid TransferOrderId,
    IReadOnlyList<TransferPickLineDto> Lines,
    string UpdatedBy
) : IRequest<Result>;
