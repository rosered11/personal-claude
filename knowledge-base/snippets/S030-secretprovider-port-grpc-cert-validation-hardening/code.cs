// ============================================================================
// File 1 of 3: Order.Contracts/Secrets/ISecretProvider.cs
// The PORT. Lives in the service's *.Contracts assembly (no EF/Npgsql refs).
// Callers depend only on this interface -- never on where a secret actually
// lives (env var, secrets manager, or legacy appsettings fallback).
// ============================================================================
namespace Order.Contracts.Secrets;

public interface ISecretProvider
{
    /// <summary>
    /// Returns the secret value for <paramref name="key"/>, or throws
    /// <see cref="SecretNotFoundException"/> if no source (environment,
    /// secrets manager, or legacy fallback) has it.
    /// </summary>
    Task<string> GetSecretAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns the raw bytes of a trusted CA certificate used to pin gRPC/TLS
    /// validation for internal service-to-service calls.
    /// </summary>
    Task<byte[]> GetTrustedCaCertificateAsync(string key, CancellationToken ct = default);
}

public sealed class SecretNotFoundException : Exception
{
    public SecretNotFoundException(string key)
        : base($"Secret '{key}' was not found in any configured source (environment, " +
               "secrets manager, or legacy appsettings fallback).")
    {
    }
}

// ============================================================================
// File 2 of 3: Order.Infrastructure/Secrets/EnvironmentSecretProvider.cs
// The ADAPTER. Lives in *.Infrastructure. Prefers environment variables (the
// target state); falls back to the legacy IConfiguration-bound appsettings
// value only if the env var is absent, and LOGS every fallback use so the
// remaining plaintext-secret surface stays visible instead of silent.
//
// This is what makes the P025/D030 migration zero-disruption: nothing breaks
// on day one, but every appsettings-sourced secret shows up in logs until it
// is rotated and moved to a real source.
// ============================================================================
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Order.Contracts.Secrets;

namespace Order.Infrastructure.Secrets;

public sealed class EnvironmentSecretProvider : ISecretProvider
{
    private readonly IConfiguration _legacyConfig;
    private readonly ILogger<EnvironmentSecretProvider> _logger;

    public EnvironmentSecretProvider(
        IConfiguration legacyConfig,
        ILogger<EnvironmentSecretProvider> logger)
    {
        _legacyConfig = legacyConfig;
        _logger = logger;
    }

    public Task<string> GetSecretAsync(string key, CancellationToken ct = default)
    {
        // Target state: environment variable, e.g. ORDER_DB_CONNECTION_STRING.
        var envKey = ToEnvironmentVariableName(key);
        var fromEnv = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return Task.FromResult(fromEnv);
        }

        // Interim fallback: read the legacy appsettings*.json value, but log
        // loudly every time this path is used so it cannot silently persist.
        var fromLegacyConfig = _legacyConfig[key];
        if (!string.IsNullOrEmpty(fromLegacyConfig))
        {
            _logger.LogWarning(
                "Secret '{Key}' was read from legacy appsettings configuration, not from an " +
                "environment variable or secrets manager. This is a P025-flagged plaintext " +
                "secret pending rotation -- migrate '{EnvKey}' to a real secret source.",
                key, envKey);
            return Task.FromResult(fromLegacyConfig);
        }

        throw new SecretNotFoundException(key);
    }

    public async Task<byte[]> GetTrustedCaCertificateAsync(string key, CancellationToken ct = default)
    {
        // Certificates are stored base64-encoded wherever the underlying
        // secret value lives (env var or legacy config), same resolution
        // order as GetSecretAsync.
        var base64 = await GetSecretAsync(key, ct);
        return Convert.FromBase64String(base64);
    }

    private static string ToEnvironmentVariableName(string key) =>
        key.Replace(':', '_').Replace('.', '_').ToUpperInvariant();
}

// ============================================================================
// File 3 of 3: Order.Integration/DependencyInjection.cs
// Interim gRPC certificate-validation hardening: replaces
// DangerousAcceptAnyServerCertificateValidator with real chain validation
// pinned to an internal CA thumbprint sourced via ISecretProvider (never
// hardcoded). Registered once per gRPC client; safe to roll out service by
// service without renaming any existing gRPC contract.
// ============================================================================
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Order.Contracts.Secrets;

namespace Order.Integration;

public static class DependencyInjection
{
    /// <summary>
    /// Registers a gRPC channel with real certificate-chain validation pinned
    /// to an internal CA, replacing the previous
    /// DangerousAcceptAnyServerCertificateValidator that accepted any
    /// certificate unconditionally.
    /// </summary>
    public static IServiceCollection AddHardenedGrpcClient<TClient>(
        this IServiceCollection services,
        string address,
        string trustedCaSecretKey)
        where TClient : class
    {
        services.AddGrpcClient<TClient>(o => o.Address = new Uri(address))
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var secretProvider = sp.GetRequiredService<ISecretProvider>();

                // Resolve the pinned internal CA once per handler creation.
                // In production this is cached; kept simple here for clarity.
                var trustedCaBytes = secretProvider
                    .GetTrustedCaCertificateAsync(trustedCaSecretKey)
                    .GetAwaiter()
                    .GetResult();
                using var trustedCa = X509CertificateLoader.LoadCertificate(trustedCaBytes);

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, cert, chain, errors) =>
                    {
                        // BEFORE (P025 finding, ~30 gRPC clients):
                        //   ServerCertificateCustomValidationCallback =
                        //       HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                        //
                        // AFTER: real chain validation, pinned to the internal CA.
                        if (cert is null || chain is null)
                        {
                            return false;
                        }

                        chain.ChainPolicy.ExtraStore.Add(trustedCa);
                        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                        chain.ChainPolicy.CustomTrustStore.Add(trustedCa);
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                        var isValidChain = chain.Build(cert);
                        return isValidChain;
                    }
                };

                return handler;
            });

        return services;
    }
}

// ----------------------------------------------------------------------------
// Commented NetArchTest fitness-function guardrail (extends D029/S029's
// pattern to the secrets seam). Not wired up here -- shown for reference so
// the same CI-enforced-boundary approach covers secrets, not just
// cross-service Infrastructure references.
// ----------------------------------------------------------------------------
//
// [Fact]
// public void OrderCore_MustNotReadRawSecretsDirectly()
// {
//     var result = Types.InAssembly(typeof(Order.Core.AssemblyMarker).Assembly)
//         .That()
//         .DoNotResideInNamespace("Order.Infrastructure.Secrets")
//         .Should()
//         .NotHaveDependencyOn("Microsoft.Extensions.Configuration.IConfiguration")
//         .GetResult();
//
//     // Allow-list: shrink-only, seeded from any pre-existing direct
//     // IConfiguration["ConnectionStrings:*"] reads found outside
//     // Order.Infrastructure.Secrets during the P025 audit.
//     Assert.True(result.IsSuccessful, string.Join(", ",
//         result.FailingTypeNames ?? Array.Empty<string>()));
// }
