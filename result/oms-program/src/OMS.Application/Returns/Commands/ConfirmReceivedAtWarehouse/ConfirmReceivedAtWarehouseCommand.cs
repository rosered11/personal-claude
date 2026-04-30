using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Returns.Commands.ConfirmReceivedAtWarehouse;

public record ConfirmReceivedAtWarehouseCommand(
    Guid ReturnId,
    string GoodsReceiveNo,
    string UpdatedBy
) : IRequest<Result>;
