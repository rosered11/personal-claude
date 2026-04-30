using MediatR;
using OMS.Application.Common.Interfaces;
using OMS.Application.Common.Models;

namespace OMS.Application.Orders.Queries.GetOrderTimeline;

public class GetOrderTimelineHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetOrderTimelineQuery, Result<OrderTimelineDto>>
{
    public async Task<Result<OrderTimelineDto>> Handle(
        GetOrderTimelineQuery request,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderTimelineDto>.Fail($"Order '{request.OrderId}' not found.");

        var outboxEntries = await orderRepository.GetOutboxEntriesAsync(request.OrderId, cancellationToken);
        var webhookLogs = await orderRepository.GetWebhookLogsAsync(request.OrderId, cancellationToken);

        var domainEntries = order.StatusHistory
            .Select(h => new TimelineEntryDto(
                At: h.ChangedAt,
                Type: "Domain",
                Event: h.FromStatus is null
                    ? $"Created → {h.ToStatus}"
                    : $"{h.FromStatus} → {h.ToStatus}",
                Actor: h.ChangedBy,
                Detail: h.Detail));

        var inboundEntries = webhookLogs
            .Select(l => new TimelineEntryDto(
                At: l.ReceivedAt,
                Type: "WebhookReceived",
                Event: l.EventType,
                Actor: l.SourceSystem,
                Detail: l.Detail));

        var outboundEntries = outboxEntries
            .Select(o => new TimelineEntryDto(
                At: o.PublishedAt ?? o.CreatedAt,
                Type: "Outbound",
                Event: o.EventType,
                Actor: o.TargetSystem,
                Detail: o.Status == "Failed"
                    ? $"status=Failed retries={o.RetryCount}"
                    : $"status={o.Status}"));

        var timeline = domainEntries
            .Concat(inboundEntries)
            .Concat(outboundEntries)
            .OrderBy(e => e.At)
            .ToList();

        return Result<OrderTimelineDto>.Ok(new OrderTimelineDto(
            OrderId: order.OrderId,
            OrderNumber: order.OrderNumber,
            CurrentStatus: order.Status.ToString(),
            Timeline: timeline));
    }
}
