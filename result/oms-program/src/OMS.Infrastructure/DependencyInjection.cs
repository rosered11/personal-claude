using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OMS.Application.Common.Interfaces;
using OMS.Infrastructure.Adapters;
using OMS.Infrastructure.Inbound;
using OMS.Infrastructure.Outbox;
using OMS.Infrastructure.Persistence;
using OMS.Infrastructure.Persistence.Repositories;
using OMS.Infrastructure.Services;

namespace OMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException("Connection string 'OrdersDb' is not configured.");

        services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName)));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IReturnRepository, ReturnRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<ITransferOrderRepository, TransferOrderRepository>();
        services.AddScoped<IOutboxPublisher, OutboxPublisher>();
        services.AddScoped<IWebhookEventLogger, WebhookEventLogger>();
        services.AddScoped<IFulfillmentRouter, FulfillmentRouter>();

        services.AddScoped<IWmsAdapter, StubWmsAdapter>();
        services.AddScoped<ITmsAdapter, StubTmsAdapter>();
        services.AddScoped<IPosAdapter, StubPosAdapter>();

        services.AddHostedService<OutboxWorker>();

        return services;
    }
}
