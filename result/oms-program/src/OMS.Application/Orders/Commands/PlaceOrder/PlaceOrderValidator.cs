using FluentValidation;

namespace OMS.Application.Orders.Commands.PlaceOrder;

public class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.OrderNumber)
            .NotEmpty().WithMessage("Order number is required.")
            .MaximumLength(100).WithMessage("Order number must not exceed 100 characters.");

        RuleFor(x => x.BusinessUnit)
            .NotEmpty().WithMessage("Business unit is required.");

        RuleFor(x => x.StoreId)
            .NotEmpty().WithMessage("Store ID is required.");

        RuleFor(x => x.CreatedBy)
            .NotEmpty().WithMessage("Created by is required.");

        RuleFor(x => x.OrderLines)
            .NotEmpty().WithMessage("At least one order line is required.");

        RuleForEach(x => x.OrderLines).ChildRules(line =>
        {
            line.RuleFor(l => l.Sku).NotEmpty().WithMessage("SKU is required.");
            line.RuleFor(l => l.ProductName).NotEmpty().WithMessage("Product name is required.");
            line.RuleFor(l => l.RequestedAmount).GreaterThan(0).WithMessage("Requested amount must be greater than zero.");
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.");
            line.RuleFor(l => l.Currency).NotEmpty().WithMessage("Currency is required.");
        });
    }
}
