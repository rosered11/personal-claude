using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Queries.GetOrder;

public record GetOrderQuery(Guid OrderId) : IRequest<Result<OrderDto>>;
