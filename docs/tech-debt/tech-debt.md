# Authora – Tech Debt

Known shortcuts and temporary decisions that must be resolved later. Each entry explains what the shortcut is, why it was taken, and what is needed to remove it.

| ID | Title | Status |
|---|---|---|
| [TD-001](td-001.md) | Users can be created without a password | Open |
| [TD-002](td-002.md) | Disabling a Client does not revoke its Refresh Tokens | Resolved (2026-10-11) |
| [TD-003](td-003.md) | The admin API has no authentication or authorization | Open |
| [TD-004](td-004.md) | The last-administrator check is not safe against concurrent requests | Open |
| [TD-005](td-005.md) | The Email of a deleted User can never be used again | Open |
| [TD-006](td-006.md) | Removing Roles or Permissions does not invalidate existing Sessions and tokens | Open |
| [TD-007](td-007.md) | Browser-based Clients cannot call the token endpoint (no CORS) | Open |
| [TD-008](td-008.md) | Expired codes, tokens and Sessions are never deleted | Open |
| [TD-009](td-009.md) | Every Confidential Client can introspect every Access Token | Open |

## Adding an entry

1. Create `td-<number>.md` with the next free number, using an existing entry as a template.
2. Fill in `Status`, `Added` and `Area`, and the sections Current state, Why, Impact and How to resolve.
3. Add a row to the table above.

When an entry is resolved, set its `Status` to `Resolved (<date>)` in the entry and in the table, and keep the file for history.
