using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Queries.ListOrders;

public record ListOrdersQuery(
    string? Status,
    Guid? StoreId,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PaginatedResult<OrderDto>>>;
