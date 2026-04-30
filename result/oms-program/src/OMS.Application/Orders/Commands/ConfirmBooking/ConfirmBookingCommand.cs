using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.ConfirmBooking;

public record ConfirmBookingCommand(Guid OrderId, string UpdatedBy) : IRequest<Result>;
