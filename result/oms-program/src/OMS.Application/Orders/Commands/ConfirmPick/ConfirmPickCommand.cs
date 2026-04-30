using MediatR;
using OMS.Application.Common.Models;
using OMS.Domain.Enums;

namespace OMS.Application.Orders.Commands.ConfirmPick;

public record PickedLineDto(Guid OrderLineId, decimal PickedAmount);

public record SubstitutionDto(
    Guid OriginalOrderLineId,
    string SubstituteSku,
    string SubstituteProductName,
    string SubstituteBarcode,
    UnitOfMeasure SubstituteUnitOfMeasure,
    decimal SubstituteUnitPrice,
    decimal SubstitutedAmount);

public record ConfirmPickCommand(
    Guid OrderId,
    IReadOnlyList<PickedLineDto> PickedLines,
    IReadOnlyList<SubstitutionDto> Substitutions,
    string UpdatedBy
) : IRequest<Result>;
