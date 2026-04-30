using OMS.Domain.Enums;

namespace OMS.Domain.Exceptions;

public class InvalidStateTransitionException : OrderDomainException
{
    public OrderStatus CurrentStatus { get; }
    public string AttemptedAction { get; }

    public InvalidStateTransitionException(OrderStatus currentStatus, string attemptedAction)
        : base($"Cannot perform '{attemptedAction}' when order is in status '{currentStatus}'.")
    {
        CurrentStatus = currentStatus;
        AttemptedAction = attemptedAction;
    }
}
