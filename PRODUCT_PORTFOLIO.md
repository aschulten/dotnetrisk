# DotnetRisk Product Portfolio

All offers share one API, deployment, payment rail, cache, documentation surface, and monitoring stack. They are not separate applications.

## Launch group

| Priority | Offer | Input | Output | Target price |
|---|---|---|---|---:|
| 1 | NuGet Security Audit | Package IDs and versions | Vulnerabilities and remediation order | USD 0.05 |
| 2 | .NET Upgrade Planner | Target framework and packages | Compatibility and upgrade plan | USD 0.25 |
| 3 | License Risk Audit | Package IDs and versions | License obligations and conflicts | USD 0.10 |

The first two offers are implemented. Offer 3 should reuse their package metadata pipeline and launch only after payment and deployment are functional.

## Expansion group

| Priority | Offer | Input | Output | Target price |
|---|---|---|---|---:|
| 4 | SBOM Risk Report | CycloneDX or SPDX manifest | Prioritized supply-chain report | USD 5.00 |
| 5 | Dockerfile Security Review | Dockerfile text | Hardening findings and fixed file | USD 4.00 |
| 6 | GitHub Actions Audit | Workflow YAML | Permission and supply-chain findings | USD 4.00 |
| 7 | OpenAPI Security Audit | OpenAPI document | Authentication and exposure findings | USD 5.00 |
| 8 | SQL Query Risk Check | SQL statement and engine | Performance and safety findings | USD 2.00 |
| 9 | Azure Cost Configuration Check | Sanitized resource configuration | Waste and misconfiguration findings | USD 6.00 |
| 10 | Release Risk Brief | Dependency and change manifests | Machine-readable release risk summary | USD 3.00 |

## Recurring offer

After a paid single report is validated, add a combined audit at USD 0.35, monitoring at USD 5 per project per month, and a team tier at USD 19 per month. Subscription work is deferred until external demand exists.

## Release rules

1. Do not build more than three launch offers before the first external payment.
2. Do not create separate repositories or deployments for individual offers.
3. Do not collect private repositories, secrets, or full source trees.
4. Record self-payments separately and never count them as revenue.
5. Stop an offer after 30 days of correct indexing and zero external purchases.
