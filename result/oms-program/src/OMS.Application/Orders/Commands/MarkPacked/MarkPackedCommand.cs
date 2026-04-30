using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.MarkPacked;

public record MarkPackedCommand(Guid OrderId, string UpdatedBy) : IRequest<Result>;
