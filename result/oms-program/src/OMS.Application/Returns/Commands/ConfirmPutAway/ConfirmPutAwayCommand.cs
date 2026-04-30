using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Returns.Commands.ConfirmPutAway;

public record PutAwayItemDto(
    Guid ReturnItemId,
    ItemCondition Condition,
    string AssignedSloc,
    decimal Quantity,
    string PerformedBy
);

public record ConfirmPutAwayCommand(
    Guid ReturnId,
    IReadOnlyList<PutAwayItemDto> Items,
    string UpdatedBy
) : IRequest<Result>;
