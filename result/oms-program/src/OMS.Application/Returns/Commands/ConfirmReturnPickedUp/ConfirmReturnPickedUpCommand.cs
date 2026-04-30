using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Returns.Commands.ConfirmReturnPickedUp;

public record ConfirmReturnPickedUpCommand(Guid ReturnId, string UpdatedBy) : IRequest<Result>;
