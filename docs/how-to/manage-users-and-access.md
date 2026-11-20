# Manage users and access

This guide covers the everyday tasks in the admin interface: adding users, giving them administrative rights through Roles and Groups, and locking them out. Each task names the Permission it needs; the `admin` Role has all of them.

> Roles, Groups and Permissions control what a User may do **in Authora's admin API and interface**. They are not passed on to your applications. See [the access model](../explanation/access-model.md).

## Add a User

**Users** → **New User** (`users.create`). Enter the Email, first and last name, and an initial password of 12 to 128 characters. Give the password to the person through a secure channel.

## Change or set a password

Users change their own password at `/account/password` on Authora (e.g. `https://auth.example.com/account/password`), after entering the current one; the admin interface links there from the lock icon next to **Sign out**. They stay signed in in that browser and are signed out everywhere else, and if email is configured, they get a notification. A wrong current password counts towards the lockout like a failed sign-in. Share the link with your users; no Permission is needed.

Users cannot reset a forgotten password yet. To set a new one for them: open the User → **Set password** (`users.set-password`). This signs the User out everywhere, including the browser they use.

## Give someone administrative rights

The simplest way: open the User → assign the Role `admin` (`users.assign-roles`).

For narrower rights, create a Role with only the Permissions needed:

1. **Roles** → **New Role** (`roles.create`), e.g. `support` with the display name `Support`.
2. Open the Role and grant Permissions (`roles.grant-permissions`), e.g. `users.read` and `users.manage-access`. The full list is in the [admin API reference](../reference/admin-api.md#permissions).
3. Assign the Role to Users, or to a Group.

The `admin` Role is a System Role: it always has every Permission, its Permissions cannot be changed, and it cannot be deleted or assigned to a Group. Authora also refuses to remove the last User who can still sign in as an administrator.

## Use Groups

A Group gives all its members the Roles assigned to the Group.

1. **Groups** → **New Group** (`groups.create`).
2. Open the Group, assign Roles (`groups.assign-roles`), and add members by searching for Users (`groups.manage-members`).

A User's page shows their effective Permissions and where each one comes from: a direct Role or a Group.

Changes take effect with the next request: the admin API loads the User's Permissions on every call and never caches them, so nobody has to sign in again.

## Lock someone out

| Task | Action | Effect |
|---|---|---|
| Temporarily block | **Deactivate** (`users.manage-access`) | Signs the User out everywhere and blocks sign-in until **Activate**. |
| End all sessions | **Sign out everywhere** (`users.manage-access`) | Ends all Sessions and revokes all tokens of the User, in every application. |
| Remove for good | **Delete** (`users.delete`) | Deletes the User and all their Sessions and tokens. Their Email can be used again. |
| Undo a lockout | **Unlock** (`users.manage-access`) | After 5 failed sign-ins in a row, a User is locked out for 15 minutes; this ends it early. |

## Find out what happened

Every page links to its **Audit log**: open a User and follow the link to see their sign-ins and every change made to them. See [Audit events](../reference/audit-events.md).
