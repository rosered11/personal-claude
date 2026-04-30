using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Inbound.TransferOrders.Commands.ConfirmTransferReceived;

public record ConfirmTransferReceivedCommand(
    Guid TransferOrderId,
    string UpdatedBy
) : IRequest<Result>;
