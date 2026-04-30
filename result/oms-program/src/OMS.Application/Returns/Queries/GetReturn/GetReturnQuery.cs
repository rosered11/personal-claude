using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Returns.Queries.GetReturn;

public record GetReturnQuery(Guid ReturnId) : IRequest<Result<ReturnDto>>;
