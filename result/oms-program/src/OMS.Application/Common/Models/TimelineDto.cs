namespace OMS.Application.Common.Models;

public record OrderTimelineDto(
    Guid OrderId,
    string OrderNumber,
    string CurrentStatus,
    IReadOnlyList<TimelineEntryDto> Timeline
);

public record TimelineEntryDto(
    DateTimeOffset At,
    string Type,
    string Event,
    string Actor,
    string? Detail
);
