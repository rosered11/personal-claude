using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.RejectSubstitution;

public record RejectSubstitutionCommand(
    Guid OrderId,
    Guid SubstitutionId,
    string UpdatedBy
) : IRequest<Result>;
