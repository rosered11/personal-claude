---
name: Sprint-OMS Deployment and gRPC Security Context
description: Ground-truth facts about Sprint-OMS (D:\workspace\Sprint-OMS) infra/deployment and inter-service gRPC security, useful for any future consultation touching this repo
type: project
---

# Sprint-OMS Deployment Facts (verified by reading source, 2026-07-31)

- Deployment target IS Kubernetes: `azure-pipelines.yml` deploys to Tencent Kubernetes Engine (TKE) — product name `spoms-tke`. Manifests live in external repos (`k8s-config-tke`, `product-k8s-config`), NOT inside the Sprint-OMS repo itself. `kube-oms/` in this repo only hosts `mock-api` and `web-ui` — it is NOT the manifest set for Order/Portal/Master/Front/Report.
- This means: any option assuming "no Kubernetes" is wrong — a service mesh (sidecar-based, e.g. Istio/Linkerd) IS infrastructurally feasible here, though the actual mesh installation would happen in the external k8s-config repos we don't have direct visibility into from this repo.
- Every gRPC client registration across Order.Integration, Portal.Integration, Master.Integration (~30+ `AddGrpcClient<T>` call sites) hardcodes `ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator` because each service's cert (e.g. `oms-order-internal.crt`) is self-signed with no CA chain. This is a systemic, repeated security gap — not a one-off — and is a strong, concrete argument for mesh-issued mTLS (SPIFFE-based sidecar certs) replacing self-signed certs + disabled validation everywhere at once.
- `CurrentUserGrpcClientInterceptor` / `CurrentUserGrpcServerInterceptor` (in `Shared.Infrastructure.Grpc`) propagate the authenticated end-user identity across service boundaries via gRPC metadata. This is an APPLICATION-layer concern (business identity, not transport identity) — a service mesh does NOT replace this interceptor; mesh mTLS only secures workload-to-workload transport. Don't oversell mesh as solving end-user auth propagation.
- Grep for Polly/CircuitBreaker/RetryPolicy across the repo only matches `.csproj`/`.lscache` package references — there is NO actual app-layer retry/circuit-breaker/timeout code configured for the ~30+ gRPC clients. This is a second strong argument for mesh (Envoy-level VirtualService/DestinationRule retries, timeouts, circuit breaking) vs. asking every consumer team to hand-roll Polly policies per client.
- Plaintext committed secrets confirmed firsthand: `Order/Order.API/appsettings.AzureDevelop.json` contains `Host=scm-bkk-spwms-psql-01-nonprd.central.co.th;...;Password=Pcsnon#p4d` in cleartext. Real infra credential, must be rotated not just removed from git history (matches problem constraint).
