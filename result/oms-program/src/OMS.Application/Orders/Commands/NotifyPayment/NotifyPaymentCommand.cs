using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.NotifyPayment;

public record NotifyPaymentCommand(Guid OrderId, string UpdatedBy) : IRequest<Result>;
