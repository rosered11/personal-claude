using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.MarkReadyForCollection;

public record MarkReadyForCollectionCommand(Guid OrderId, string UpdatedBy) : IRequest<Result>;
