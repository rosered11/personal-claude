---
when_to_use: "When a service currently reads secrets straight out of appsettings*.json and/or disables gRPC/TLS certificate validation (e.g. DangerousAcceptAnyServerCertificateValidator) for internal cross-service calls, and you need a zero-disruption, incrementally-adoptable seam to rotate secrets and re-enable real certificate validation without renaming existing gRPC contracts."
related_problems: [P025]
related_decisions: [D030]
---

# Snippet: ISecretProvider Port + Interim gRPC Certificate-Validation Hardening

## What this demonstrates

P025's audit found plaintext credentials committed to source-controlled `appsettings*.json` files
across multiple Sprint-OMS services, and roughly 30 gRPC clients that disable TLS certificate
validation entirely via `DangerousAcceptAnyServerCertificateValidator`. Both problems share the
same root cause: there is no abstraction seam through which a service asks for a secret or a
trusted certificate, so the path of least resistance under AI-assisted "vibe coding" was to inline
the value or bypass the check.

This snippet implements the Hexagonal fix chosen in D030:

- `ISecretProvider` (port) -- lives in the service's `*.Contracts` assembly. Callers ask for a
  named secret; they never know or care whether it comes from an environment variable, a secrets
  manager, or (temporarily, with a loud warning) a legacy `appsettings` value.
- `EnvironmentSecretProvider` (adapter) -- lives in `*.Infrastructure`. Reads from environment
  variables first (the target state), and falls back to the legacy `IConfiguration`-bound
  appsettings value only if the environment variable is absent, logging a warning each time the
  fallback path is used. This is what makes the migration zero-disruption: nothing breaks on day
  one, but every remaining plaintext-secret usage becomes visible in logs instead of silent.
- gRPC channel wiring (`DependencyInjection.cs`) -- replaces
  `DangerousAcceptAnyServerCertificateValidator` with a real `RemoteCertificateValidationCallback`
  that validates the chain and pins it to an internal CA thumbprint sourced through
  `ISecretProvider` (never hardcoded), rather than accepting any certificate unconditionally.
- A commented NetArchTest fitness-function guardrail showing how this seam is enforced in CI going
  forward, consistent with the pattern already adopted in D029/S029 for the Infrastructure-assembly
  boundary rule.

## How to extend it

- Swap `EnvironmentSecretProvider` for a real secrets-manager-backed adapter (e.g. Azure Key
  Vault, HashiCorp Vault) once available -- the port (`ISecretProvider`) and every call site stay
  unchanged, which is the point of the Hexagonal seam.
- Add one `ISecretProvider.GetSecretAsync("...")` call per confirmed plaintext-secret finding from
  P025 (Portal.API MySQL/Redis, Order.API PostgreSQL), removing the corresponding value from
  `appsettings*.json` once each is migrated and rotated.
- Extend the NetArchTest guardrail with a rule forbidding direct `IConfiguration["ConnectionStrings:*"]`
  or similar raw-secret reads outside the `*.Infrastructure` adapter layer, so future AI-assisted
  changes cannot reintroduce a hardcoded secret.
- When Phase 2 (Service Mesh, per D030) lands, the interim certificate-pinning logic here can be
  simplified or removed once mTLS is enforced at the proxy layer instead of in application code.
