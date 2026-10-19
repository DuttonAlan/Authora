# Authora documentation

The documentation follows the [Diátaxis](https://diataxis.fr/) framework and has four parts. Each part answers a different kind of question, so pick the one that fits what you need right now.

| | Part | Read it when you want to… |
|---|---|---|
| 🎓 | [Tutorials](#tutorials) | learn Authora by doing, step by step, from nothing to a working result |
| 🛠️ | [How-to guides](#how-to-guides) | get a specific job done, e.g. deploy Authora or rotate a key |
| 📖 | [Reference](#reference) | look up exact facts: settings, endpoints, claims, events |
| 💡 | [Explanation](#explanation) | understand how Authora works and why it is designed that way |

## Tutorials

Start here if you are new to Authora.

1. [Getting started](tutorials/getting-started.md): run Authora with Docker Compose and sign in to the admin interface.
2. [Your first application](tutorials/first-application.md): register an application, sign a user in with the Authorization Code flow and PKCE, and read the ID Token and UserInfo.

## How-to guides

Running Authora:

- [Deploy to production](how-to/deploy-to-production.md)
- [Configure a reverse proxy](how-to/configure-a-reverse-proxy.md)
- [Apply database migrations and upgrade](how-to/upgrade-authora.md)
- [Persist Data Protection keys](how-to/persist-data-protection-keys.md)
- [Rotate signing keys](how-to/rotate-signing-keys.md)
- [Back up and restore](how-to/back-up-and-restore.md)
- [Run Authora from source](how-to/run-from-source.md)

Using Authora:

- [Connect an application](how-to/connect-an-application.md)
- [Protect an API with introspection](how-to/protect-an-api.md)
- [Manage users and access](how-to/manage-users-and-access.md)
- [Call the admin API](how-to/call-the-admin-api.md)
- [Check the integrity of the audit log](how-to/verify-the-audit-log.md)

## Reference

- [Configuration](reference/configuration.md): every setting, its default and its limits
- [OAuth endpoints](reference/oauth-endpoints.md): authorize, token, revoke, introspect
- [OpenID Connect](reference/openid-connect.md): discovery, Scopes and claims, ID Tokens, UserInfo, JWKS
- [Admin API](reference/admin-api.md): endpoints and the Permission each one needs
- [Audit events](reference/audit-events.md): event types, fields, search and verification
- [HTTP security headers and cookies](reference/http-security.md)
- [Health checks](reference/health-checks.md)
- [Log events](reference/log-events.md)
- [Standards and limitations](reference/standards.md)

## Explanation

- [Architecture](explanation/architecture.md)
- [The access model: Users, Groups, Roles, Permissions, Scopes and Clients](explanation/access-model.md)
- [Tokens, Grants and Sessions](explanation/tokens-and-sessions.md)
- [Security model](explanation/security-model.md)
- [The admin interface](explanation/admin-interface.md)
- [Audit log integrity](explanation/audit-log-integrity.md)

## Terms

Authora capitalizes the names of its own objects (User, Group, Role, Permission, Scope, Client, Grant, Session) to tell them apart from the everyday words. A *Client* is an application registered at Authora; a *User* is a person who signs in to Authora.
