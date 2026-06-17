// S021 - RabbitMQ IHostedService Hexagonal Log Consumer
// DataMartQueryService - Activity Transaction Log Ingestion
// Stack: .NET 8, RabbitMQ.Client, EF Core 8 + Npgsql, PostgreSQL

// ---- 1. ENTITY (DataMartQueryService.Infrastructure/Entities) ----
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public sealed class ActivityTransactionLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    [MaxLength(100)] public string? TransactionID { get; set; }
    [MaxLength(100)] public string? ProcessActivityCode { get; set; }
    [MaxLength(40)]  public string? OrderNumber { get; set; }
    [MaxLength(50)]  public string? SourceOrderId { get; set; }
    [MaxLength(40)]  public string? SubOrderNumber { get; set; }
    [MaxLength(50)]  public string? SourceSubOrderId { get; set; }
    public DateTime RequestDate { get; set; }
    [MaxLength(300)] public string? RequestUrl { get; set; }
    [MaxLength(10)]  public string? RequestMethod { get; set; }
    public string? RequestHeader { get; set; }
    public string? RequestBody { get; set; }
    public DateTime? ResponseDate { get; set; }
    [MaxLength(50)]  public string? ResponseCode { get; set; }
    public string? ResponseHeader { get; set; }
    public string? ResponseBody { get; set; }
    [MaxLength(50)]  public string? ServiceName { get; set; }
    [MaxLength(200)] public string? Ref1 { get; set; }
    [MaxLength(200)] public string? Ref2 { get; set; }
    [MaxLength(200)] public string? Ref3 { get; set; }
    [MaxLength(200)] public string? Ref4 { get; set; }
    [MaxLength(200)] public string? Ref5 { get; set; }
}

// ---- 2. MESSAGE CONTRACT ----
public sealed class ActivityLogMessage
{
    public string?   CreatedBy { get; set; }
    public DateTime  CreatedDate { get; set; }
    public string?   TransactionID { get; set; }
    public string?   ProcessActivityCode { get; set; }
    public string?   OrderNumber { get; set; }
    public string?   SourceOrderId { get; set; }
    public string?   SubOrderNumber { get; set; }
    public string?   SourceSubOrderId { get; set; }
    public DateTime  RequestDate { get; set; }
    public string?   RequestUrl { get; set; }
    public string?   RequestMethod { get; set; }
    public string?   RequestHeader { get; set; }
    public string?   RequestBody { get; set; }
    public DateTime? ResponseDate { get; set; }
    public string?   ResponseCode { get; set; }
    public string?   ResponseHeader { get; set; }
    public string?   ResponseBody { get; set; }
    public string?   ServiceName { get; set; }
    public string?   Ref1 { get; set; }
    public string?   Ref2 { get; set; }
    public string?   Ref3 { get; set; }
    public string?   Ref4 { get; set; }
    public string?   Ref5 { get; set; }
}

// ---- 3. PORTS ----
public interface IActivityLogService
{
    Task RecordAsync(ActivityLogMessage message, CancellationToken ct = default);
}

public interface IActivityLogRepository
{
    Task InsertAsync(ActivityTransactionLog entity, CancellationToken ct = default);
}

// ---- 4. APPLICATION SERVICE (manual mapping, no AutoMapper) ----
public sealed class ActivityLogService : IActivityLogService
{
    private readonly IActivityLogRepository _repo;
    public ActivityLogService(IActivityLogRepository repo) => _repo = repo;

    public async Task RecordAsync(ActivityLogMessage msg, CancellationToken ct = default)
    {
        var entity = new ActivityTransactionLog
        {
            CreatedBy           = msg.CreatedBy,
            CreatedDate         = msg.CreatedDate == default ? DateTime.UtcNow : msg.CreatedDate,
            TransactionID       = msg.TransactionID,
            ProcessActivityCode = msg.ProcessActivityCode,
            OrderNumber         = msg.OrderNumber,
            SourceOrderId       = msg.SourceOrderId,
            SubOrderNumber      = msg.SubOrderNumber,
            SourceSubOrderId    = msg.SourceSubOrderId,
            RequestDate         = msg.RequestDate,
            RequestUrl          = msg.RequestUrl,
            RequestMethod       = msg.RequestMethod,
            RequestHeader       = msg.RequestHeader,
            RequestBody         = msg.RequestBody,
            ResponseDate        = msg.ResponseDate,
            ResponseCode        = msg.ResponseCode,
            ResponseHeader      = msg.ResponseHeader,
            ResponseBody        = msg.ResponseBody,
            ServiceName         = msg.ServiceName,
            Ref1 = msg.Ref1, Ref2 = msg.Ref2, Ref3 = msg.Ref3,
            Ref4 = msg.Ref4, Ref5 = msg.Ref5
        };
        await _repo.InsertAsync(entity, ct);
    }
}

// ---- 5. REPOSITORY ----
public sealed class ActivityLogRepository : IActivityLogRepository
{
    private readonly DataMartContext _db;
    public ActivityLogRepository(DataMartContext db) => _db = db;

    public async Task InsertAsync(ActivityTransactionLog entity, CancellationToken ct = default)
    {
        await _db.ActivityTransactionLogs.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }
}

// ---- 6. DATAMART CONTEXT ADDITIONS ----
// Add to DataMartContext.cs:
//   public DbSet<ActivityTransactionLog> ActivityTransactionLogs => Set<ActivityTransactionLog>();
//
// Add to OnModelCreating:
//   modelBuilder.Entity<ActivityTransactionLog>(b => {
//       b.ToTable("ActivityTransactionLogTb");
//       b.HasIndex(x => x.TransactionID)
//        .IsUnique()
//        .HasFilter("\"TransactionID\" IS NOT NULL");
//   });

// ---- 7. INBOUND ADAPTER - BackgroundService ----
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

public sealed class RabbitMqActivityLogConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<RabbitMqActivityLogConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqActivityLogConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<RabbitMqActivityLogConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "localhost",
            UserName = _config["RabbitMQ:User"] ?? "guest",
            Password = _config["RabbitMQ:Password"] ?? "guest",
            AutomaticRecoveryEnabled = true,
            DispatchConsumersAsync = true
        };
        _connection = factory.CreateConnection("datamart-activity-log-consumer");
        _channel = _connection.CreateModel();
        var queueName = _config["RabbitMQ:QueueName"] ?? "activity-logs";
        _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object> { { "x-dead-letter-exchange", queueName + ".dlx" } });
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
            try
            {
                var msg = JsonSerializer.Deserialize<ActivityLogMessage>(
                    ea.Body.Span,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (msg is not null)
                    await svc.RecordAsync(msg, stoppingToken);
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process activity log. DeliveryTag={DeliveryTag}",
                    ea.DeliveryTag);
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };
        _channel.BasicConsume(queueName, autoAck: false, consumer);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override void Dispose()
    {
        _channel?.Close(); _channel?.Dispose();
        _connection?.Close(); _connection?.Dispose();
        base.Dispose();
    }
}

// ---- 8. DI REGISTRATION (Program.cs) ----
// builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
// builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
// builder.Services.AddHostedService<RabbitMqActivityLogConsumer>();

// ---- 9. CONFIGURATION (appsettings.json) ----
// "RabbitMQ": { "Host": "rabbitmq-host", "User": "guest", "Password": "guest", "QueueName": "activity-logs" }

// ---- 10. MIGRATION ----
// dotnet ef migrations add AddActivityTransactionLog --project DataMartQueryService.Infrastructure --startup-project DataMartQueryService.Api
// dotnet ef database update
