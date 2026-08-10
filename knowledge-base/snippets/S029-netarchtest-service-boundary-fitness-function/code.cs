using NetArchTest.Rules;
using Xunit;

namespace Sprint.OMS.ArchitectureTests;

/// <summary>
/// CI-enforced service-boundary fitness function for Sprint-OMS.
///
/// P024 confirmed that Order.Core, Portal.Core, and Front.Core actively import other services'
/// *.Infrastructure / *.Integration assemblies (e.g. Order.Core -> Portal.Infrastructure.
/// Interfaces.TMS.ITmsPostponeService) despite a real gRPC seam already existing for the same
/// domains. This test makes that rule executable: a service's Core project may depend on its own
/// Infrastructure/Integration and on Shared.Infrastructure, but not on another service's
/// Infrastructure/Integration assembly.
///
/// The allow-list below is seeded from the exact violations found in the P024 audit so the build
/// does not break on adoption. It must only shrink as each seam is migrated to a locally-owned
/// port + gRPC adapter (D029) -- never grow. Add a violation here only alongside a ticket that
/// tracks its removal.
/// </summary>
public class ServiceBoundaryFitnessTests
{
    // Seeded 2026-07-31 from P024. Format: "{Namespace}::{ForbiddenDependency}".
    // Counts are file-level, from the P024 `using`-statement audit, kept here for traceability.
    private static readonly HashSet<string> KnownViolations = new()
    {
        "Order.Core::Portal.Infrastructure",   // 10 files (e.g. PostponeDeliveryService.cs)
        "Order.Core::Master.Infrastructure",   // 18 files
        "Front.Core::Order.Infrastructure",    // 26 files
        "Front.Core::Portal.Infrastructure",   // 10 files
        "Front.Core::Master.Infrastructure",   //  2 files
        "Portal.Core::Master.Infrastructure",  // pre-existing, not separately counted in P024
        "Portal.Core::Order.Infrastructure",   // pre-existing, not separately counted in P024
    };

    public static IEnumerable<object[]> ServiceCoreProjects => new List<object[]>
    {
        new object[] { "Order.Core", new[] { "Order" } },
        new object[] { "Portal.Core", new[] { "Portal" } },
        new object[] { "Master.Core", new[] { "Master" } },
        new object[] { "Front.Core", new[] { "Front" } },
        new object[] { "Report.Core", new[] { "Report" } },
    };

    [Theory]
    [MemberData(nameof(ServiceCoreProjects))]
    public void CoreProject_MustNotDependOn_AnotherServicesInfrastructureOrIntegration(
        string coreAssemblyName, string[] ownServiceNames)
    {
        var otherServices = new[] { "Order", "Portal", "Master", "Front", "Report" }
            .Except(ownServiceNames);

        var forbiddenNamespacePrefixes = otherServices
            .SelectMany(svc => new[] { $"{svc}.Infrastructure", $"{svc}.Integration" })
            .ToArray();

        var assembly = System.Reflection.Assembly.Load(coreAssemblyName);

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbiddenNamespacePrefixes)
            .GetResult();

        if (result.IsSuccessful)
            return;

        // Fail only for violations NOT already tracked in the shrink-only allow-list above --
        // this is what lets the fitness function be adopted today without a big-bang cleanup.
        var newViolations = result.FailingTypes
            .Select(t => $"{coreAssemblyName}::{DependencyOf(t, forbiddenNamespacePrefixes)}")
            .Where(v => !KnownViolations.Contains(v))
            .Distinct()
            .ToList();

        Assert.True(newViolations.Count == 0,
            $"New cross-service boundary violation(s) introduced, not in the P024 allow-list: " +
            $"{string.Join(", ", newViolations)}. Route this call through the service's own " +
            $"port + gRPC adapter (see D029) instead of referencing the other service's " +
            $"Infrastructure/Integration assembly directly.");
    }

    private static string DependencyOf(NetArchTest.Rules.TypeDefinition type, string[] prefixes) =>
        prefixes.FirstOrDefault(p => type.Namespace?.StartsWith(p) == true) ?? "unknown";
}
