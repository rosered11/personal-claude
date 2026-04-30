using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;
using OMS.Domain.Exceptions;

namespace OMS.Application.Returns.Commands.ConfirmReturnPickedUp;

public class ConfirmReturnPickedUpHandler(
    IReturnRepository returnRepository,
    IOutboxPublisher outboxPublisher,
    IWebhookEventLogger webhookLogger) : IRequestHandler<ConfirmReturnPickedUpCommand, Result>
{
    public async Task<Result> Handle(ConfirmReturnPickedUpCommand request, CancellationToken cancellationToken)
    {
        var ret = await returnRepository.GetByIdAsync(request.ReturnId, cancellationToken);
        if (ret is null)
            return Result.Fail($"Return '{request.ReturnId}' not found.");

        try
        {
            webhookLogger.Stage(ret.OrderId, "TMS", "ReturnPickupConfirmed",
                $"returnId={request.ReturnId}");

            ret.ConfirmPickedUp(request.UpdatedBy);

            foreach (var evt in ret.DomainEvents)
                await outboxPublisher.PublishAsync(ret.OrderId, evt, cancellationToken);
            ret.ClearDomainEvents();

            await returnRepository.SaveAsync(ret, cancellationToken);
            return Result.Ok();
        }
        catch (ReturnDomainException ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
