using MediatR;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Commands.GenerateInvoice;

public record GenerateInvoiceCommand(Guid OrderId, string InvoiceNumber, string UpdatedBy) : IRequest<Result>;
