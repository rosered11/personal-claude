using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Inbound.DamagedGoods.Commands.ConfirmDamagedGoodsPutAway;

public record DamagedItemPutAwayDto(string Sku, ItemCondition Condition, string AssignedSloc, decimal Quantity);

public record ConfirmDamagedGoodsPutAwayCommand(
    Guid OrderId,
    string TrackingId,
    IReadOnlyList<DamagedItemPutAwayDto> Items,
    string UpdatedBy
) : IRequest<Result>;
