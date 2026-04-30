using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.ApplyPosRecalculation;

public record ApplyPosRecalculationCommand(Guid OrderId, string UpdatedBy) : IRequest<Result>;
