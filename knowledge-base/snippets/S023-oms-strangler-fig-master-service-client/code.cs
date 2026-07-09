// IMasterServiceClient.cs -- the seam being strangled.
// Order.API depends on this port only; the in-process (legacy) and HTTP (target)
// implementations are swapped via config, one call-site at a time (D023 / S023).
public interface IMasterServiceClient
{
    Task<MasterProductDto> GetProductAsync(string sku, CancellationToken ct);
}

public sealed class MasterProductDto
{
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

// Legacy adapter: today's actual behavior -- a direct in-process call into Master.Core
// via the existing IHandler convention. No network hop; kept so other seams keep working
// unmodified while this one seam is strangled.
public sealed class InProcessMasterServiceClient : IMasterServiceClient
{
    private readonly IMasterProductHandler _handler;

    public InProcessMasterServiceClient(IMasterProductHandler handler) => _handler = handler;

    public Task<MasterProductDto> GetProductAsync(string sku, CancellationToken ct) =>
        _handler.GetProductAsync(sku, ct);
}

// Target adapter: real network boundary, introduced per-seam as each strangle completes.
// Carries the W3C traceparent header (via HttpClient's built-in Activity propagation) so
// Gateway -> BFF -> Order -> Master is one OpenTelemetry trace, per D023 step 2.
public sealed class HttpMasterServiceClient : IMasterServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpMasterServiceClient> _log;

    public HttpMasterServiceClient(HttpClient http, ILogger<HttpMasterServiceClient> log)
    {
        _http = http; // registered below with Polly retry + circuit breaker
        _log = log;
    }

    public async Task<MasterProductDto> GetProductAsync(string sku, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"/api/v1/products/{sku}", ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MasterProductResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Master.API returned empty body for SKU {sku}");

        // Explicit manual mapping -- no AutoMapper, per repo-wide .NET standard.
        return new MasterProductDto
        {
            Sku = payload.Sku,
            Name = payload.Name,
            IsActive = payload.IsActive
        };
    }
}

public sealed class MasterProductResponse
{
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

// Program.cs (Order.API) -- per-seam feature flag decides legacy vs strangled implementation.
// "MasterService:Strangled:Product" flips independently of other seams (Portal, Pricing, etc.)
// so each call-site can be migrated and rolled back in isolation (D023).
public static class MasterServiceClientRegistration
{
    public static void AddMasterServiceClient(this IServiceCollection services, IConfiguration config)
    {
        var strangleProductSeam = config.GetValue<bool>("MasterService:Strangled:Product");

        if (strangleProductSeam)
        {
            services
                .AddHttpClient<IMasterServiceClient, HttpMasterServiceClient>(client =>
                {
                    client.BaseAddress = new Uri(config["MasterService:BaseUrl"]!);
                })
                .AddPolicyHandler(Policy<HttpResponseMessage>
                    .Handle<HttpRequestException>()
                    .OrResult(r => (int)r.StatusCode >= 500)
                    .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))))
                .AddPolicyHandler(Policy<HttpResponseMessage>
                    .Handle<HttpRequestException>()
                    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
        }
        else
        {
            services.AddScoped<IMasterServiceClient, InProcessMasterServiceClient>();
        }
    }
}
