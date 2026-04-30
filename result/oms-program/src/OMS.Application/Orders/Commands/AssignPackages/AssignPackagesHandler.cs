using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Orders.Commands.AssignPackages;

public class AssignPackagesHandler(
    IOrderRepository orderRepository,
    IOutboxPublisher outboxPublisher) : IRequestHandler<AssignPackagesCommand, Result>
{
    public async Task<Result> Handle(AssignPackagesCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Fail($"Order '{request.OrderId}' not found.");

        try
        {
            order.AssignPackages(
                request.Packages.Select(p => (
                    p.TrackingId,
                    p.VehicleType,
                    p.PackageWeight,
                    (IEnumerable<Guid>)p.OrderLineIds,
                    p.CarrierPackageId,
                    p.ThirdPartyLogistic,
                    p.DeliveryNoteNumber
                )),
                request.UpdatedBy);

            foreach (var evt in order.DomainEvents)
                await outboxPublisher.PublishAsync(order.OrderId, evt, cancellationToken);
            order.ClearDomainEvents();

            await orderRepository.SaveAsync(order, cancellationToken);
            return Result.Ok();
        }
        catch (OrderDomainException ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
