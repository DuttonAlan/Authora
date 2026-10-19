## What and why

<!-- What does this change, and why? Link the issue: "Closes #123". -->

## How was it tested?

<!-- New or changed tests, manual checks. -->

## Checklist

- [ ] `dotnet build Authora.slnx` and `dotnet test --solution Authora.slnx` pass
- [ ] Tests cover the change, especially new security rules
- [ ] Model changes come with a migration
- [ ] State-changing use cases record an audit event; no secrets are logged or audited
- [ ] Documentation in `docs/` is updated if behavior, configuration or endpoints changed
- [ ] `CHANGELOG.md` has an entry under *Unreleased*

<!-- Security vulnerabilities must not be fixed in a public pull request first. See SECURITY.md. -->
