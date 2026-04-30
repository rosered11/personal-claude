namespace OMS.Domain.Aggregates.OrderAggregate;

public class OrderPackageLine
{
    public Guid Id { get; private set; }
    public Guid PackageId { get; private set; }
    public Guid OrderLineId { get; private set; }

    private OrderPackageLine() { }

    public static OrderPackageLine Create(Guid packageId, Guid orderLineId) =>
        new OrderPackageLine
        {
            Id = Guid.NewGuid(),
            PackageId = packageId,
            OrderLineId = orderLineId
        };
}
