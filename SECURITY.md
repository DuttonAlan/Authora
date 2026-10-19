# Security policy

Authora handles passwords, sessions and tokens, so security reports are taken seriously and handled before other work.

## Supported versions

Authora has not reached version 1.0. Only the latest release receives security fixes. Once 1.0 is released, this table will list the supported versions.

| Version | Supported |
|---|---|
| Latest release | ✅ |
| Older releases | ❌ |
| `main` branch | Fixed there first |

## Reporting a vulnerability

**Do not open a public issue, discussion or pull request for a vulnerability.**

Report it privately through GitHub: open the [**Security** tab](https://github.com/DuttonAlan/Authora/security) of the repository and click **Report a vulnerability**. Only the maintainers can see the report.

Please include:

- the affected version or commit,
- a description of the issue and its impact,
- steps to reproduce, ideally a minimal proof of concept,
- your configuration, if relevant (never send real secrets or keys),
- whether and how you want to be credited.

## What happens next

Authora is maintained by one person in their spare time, so these are targets, not guarantees:

| Step | Target |
|---|---|
| Acknowledgement of your report | within 5 days |
| First assessment (accepted or not, severity) | within 14 days |
| Fix released for critical and high severity issues | within 90 days |

You will be kept informed in the advisory. Once a fix is released, a GitHub Security Advisory is published and, if you wish, you are credited. Please do not disclose the issue publicly before the advisory is published or 90 days have passed, whichever comes first; if more time is needed, we will ask you.

## Scope

In scope: everything in this repository, including the OAuth and OpenID Connect endpoints, the sign-in and consent pages, the admin API and admin interface, the audit log, the container images and the default configuration.

Out of scope:

- vulnerabilities that need an already compromised host, database or administrator account, unless Authora claims to protect against them (e.g. the [audit log's hash chain](docs/explanation/audit-log-integrity.md));
- missing features listed in [Standards and limitations](docs/reference/standards.md#not-supported), e.g. no multi-factor authentication;
- deployments that ignore the [production guide](docs/how-to/deploy-to-production.md), e.g. without TLS;
- denial of service through sheer request volume;
- reports from automated scanners without a demonstrated impact.

## Safe harbor

Good-faith security research on your own installation of Authora is welcome. Do not test against installations you do not own or have no permission to test, do not access or change other people's data, and do not degrade other people's services.

## Hardening

For how to run Authora securely, see [Deploy to production](docs/how-to/deploy-to-production.md) and the [security model](docs/explanation/security-model.md).
