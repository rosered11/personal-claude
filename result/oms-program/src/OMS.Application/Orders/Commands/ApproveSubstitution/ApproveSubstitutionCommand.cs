using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.ApproveSubstitution;

public record ApproveSubstitutionCommand(
    Guid OrderId,
    Guid SubstitutionId,
    string UpdatedBy
) : IRequest<Result>;
