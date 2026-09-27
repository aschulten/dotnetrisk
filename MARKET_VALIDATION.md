# DotnetRisk Market Validation

Last updated: 2026-09-27

## Finding

The original USD 2.50 dependency-audit offer is not commercially credible in the current x402 market. A direct competitor, VulnFeed, offers dependency scans for USD 0.01, individual CVE lookups for USD 0.002, project monitoring for USD 0.50 per 30 days, a 10-scan daily free tier, and a USD 14 monthly plan.

Another indexed dependency-security service, Synthr Cyber, exposes OSV, EPSS, and KEV-backed dependency audits for less than USD 0.01 and showed zero transactions, zero volume, and zero buyers during the observed 30-day window.

Observed security utilities with actual buyers were commonly priced between USD 0.01 and USD 0.10. One security gateway showed 21 transactions, USD 0.28 volume, and four buyers over 30 days. This does not support a USD 2.50 commodity dependency scan.

## Decision

Do not compete as a generic vulnerability feed. Keep the NuGet scan as a low-cost acquisition endpoint and differentiate on gaps the competitor does not advertise:

- NuGet ecosystem support;
- .NET target-framework compatibility;
- breaking-change-aware upgrade planning;
- .NET-specific license and package-maintenance risk;
- one machine-readable remediation plan across those signals.

## Pricing experiment

| Offer | Price |
|---|---:|
| NuGet Security Audit | USD 0.05 |
| .NET Upgrade Planner | USD 0.25 |
| License Risk Audit | USD 0.10 |
| Combined .NET Risk Report | USD 0.35 |
| Monitoring | USD 5/project/month |

These are hypotheses, not validated prices. Do not count self-payments or test settlements as demand.

## Evidence

- VulnFeed: https://vulnfeed.novadyne.ai/
- Synthr Cyber: https://www.x402scan.com/server/12df8283-a9df-4577-92fb-c17470036f18
- Agent Security Gateway: https://www.x402scan.com/server/84a059b0-ae89-47c9-b78d-9404ba6835f4

## Gate

The .NET Upgrade Planner MVP is deployed at https://dotnetrisk-aschulten.onrender.com and verified against live NuGet packages. Its x402 v2 payment gate is configured for native USDC on Base mainnet with a public receiving address. PayAI currently advertises support for exact payments, Base mainnet, and the Bazaar extension; its observed Base settlement price was USD 0.00231 per operation on 2026-09-27.

The public launch window started on 2026-09-27. Both paid endpoints return valid x402 v2 requirements and reject malformed payments. No self-payment was performed. Do not implement the license audit before the first external purchase.
