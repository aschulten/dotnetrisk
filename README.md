# DotnetRisk

Minimal API that turns NuGet and OSV data into a deterministic dependency-risk response.

Runtime: .NET 10.

Public API: https://dotnetrisk-aschulten.onrender.com

## Run

```powershell
dotnet run --project DotnetRisk.Api
```

Open `/swagger`, `/demo`, request `/v1/risk/{packageId}/{version}`, submit up to 25 dependencies to `POST /v1/audits`, or request a target-framework compatibility plan from `POST /v1/upgrade-plans`.

## Current scope

- Exact-version vulnerability lookup through OSV.
- Latest stable version lookup through NuGet.
- Six-hour in-memory cache.
- No repository uploads or source-code collection.
- Project-level remediation report designed for a USD 0.05 NuGet-specific acquisition offer.

The differentiated paid target is a USD 0.25 .NET upgrade plan. See `MARKET_VALIDATION.md` for current evidence and pricing assumptions.

The upgrade planner downloads package archives only from NuGet.org, limits packages to 50 MB, uses NuGet's official framework compatibility reducer, and accepts at most 10 dependencies per request.

## Payments

`POST /v1/upgrade-plans` and `POST /v1/audits` use x402 v2 with pessimistic settlement through a remote facilitator. The default configuration accepts native USDC on Base mainnet and sends proceeds to the public receiving address in `Payment:PayTo`.

The server does not hold a private key. It delegates payment verification and settlement to the configured facilitator, rejects missing or malformed payment payloads with `402`, and only returns the requested result after settlement succeeds.

Production deployment must set `Payment__PublicBaseUrl` to the public HTTPS origin so Bazaar receives an absolute externally reachable resource URL. `Payment__FacilitatorUrl`, `Payment__Network`, `Payment__Asset`, and `Payment__PayTo` can also be overridden through environment variables.
