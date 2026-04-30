using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Inbound.DamagedGoods.Commands.ConfirmDamagedGoodsReceived;

public record ConfirmDamagedGoodsReceivedCommand(
    Guid OrderId,
    string TrackingId,
    string UpdatedBy
) : IRequest<Result>;
