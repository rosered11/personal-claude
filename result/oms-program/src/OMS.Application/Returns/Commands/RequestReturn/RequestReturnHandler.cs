using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Aggregates.ReturnAggregate;
using OMS.Domain.Exceptions;

namespace OMS.Application.Returns.Commands.RequestReturn;

public class RequestReturnHandler(
    IOrderRepository orderRepository,
    IReturnRepository returnRepository,
    IOutboxPublisher outboxPublisher) : IRequestHandler<RequestReturnCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RequestReturnCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail<Guid>($"Order '{request.OrderId}' not found.");

        var existingReturn = await returnRepository.GetByReturnOrderNumberAsync(request.ReturnOrderNumber, cancellationToken);
        if (existingReturn is not null)
            return Result.Ok(existingReturn.ReturnId);

        try
        {
            var ret = OrderReturn.Create(
                request.OrderId,
                request.ReturnOrderNumber,
                request.ReturnReason,
                request.InvoiceId,
                request.CreatedBy,
                request.Items.Select(i => (
                    i.OrderLineId,
                    i.Sku,
                    i.ProductName,
                    i.Barcode,
                    i.Quantity,
                    i.UnitOfMeasure,
                    i.UnitPrice,
                    i.Currency,
                    i.PaymentMethod,
                    i.ItemReason
                )));

            foreach (var evt in ret.DomainEvents)
                await outboxPublisher.PublishAsync(request.OrderId, evt, cancellationToken);
            ret.ClearDomainEvents();

            await returnRepository.SaveAsync(ret, cancellationToken);
            return Result.Ok(ret.ReturnId);
        }
        catch (ReturnDomainException ex)
        {
            return Result.Fail<Guid>(ex.Message);
        }
    }
}
