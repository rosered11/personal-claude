using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Queries.GetOrderTimeline;

public record GetOrderTimelineQuery(Guid OrderId) : IRequest<Result<OrderTimelineDto>>;
