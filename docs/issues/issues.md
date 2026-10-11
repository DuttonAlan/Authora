# Authora – Issues

Bugs found in the code, e.g. during reviews. Each entry explains what was wrong, what it affected, why it happened and how it was fixed.

Unlike [tech debt](../tech-debt/tech-debt.md), which records shortcuts that were taken on purpose, an issue is behaviour that was not intended.

| ID | Title | Status |
|---|---|---|
| [ISS-001](iss-001.md) | Every `InvalidOperationException` is returned as 409 Conflict with its message | Resolved (2026-10-11) |
| [ISS-002](iss-002.md) | Updating a User accepts blank First and Last Names | Resolved (2026-10-11) |
| [ISS-003](iss-003.md) | Deleted Users can still be changed, including getting the administrator Role | Resolved (2026-10-11) |

## Adding an entry

1. Create `iss-<number>.md` with the next free number, using an existing entry as a template.
2. Fill in `Status`, `Found` and `Area`, and the sections Problem, Impact, Cause, Fix and Verification.
3. Add a row to the table above.

Use `Open` for issues that are not fixed yet, and `Resolved (<date>)` once the fix is merged. Keep resolved entries for history.
