# The access model

Authora answers two different questions, and it uses different objects for each:

1. **Which application may act for which user, and how far?** This is OAuth: *Clients*, *Scopes* and *Grants*.
2. **Who may administer Authora itself?** This is role-based access control: *Roles*, *Groups* and *Permissions*.

Keeping the two apart is the most important thing to understand about Authora's model.

## Users

A *User* is a person who signs in with an Email and a password. A User has a fixed Id (`sub` in tokens), which never changes, even if the Email does. Applications should recognize users by this Id.

A User is active, inactive (has not signed in for a while, but still can), deactivated (cannot sign in until an administrator activates them again) or deleted. Deleting cannot be undone: the User can never sign in again and their Email is free for a new User. The record itself is kept, so the audit log still makes sense.

## Clients, Scopes and Grants: access for applications

A *Client* is an application registered at Authora. It is either:

- **Confidential:** runs on a server and can keep a Secret, e.g. a web application's backend or an API.
- **Public:** runs on the user's device and cannot keep a Secret, e.g. a single-page app, mobile or desktop app. It proves itself with PKCE only.

A *Scope* names a kind of access, e.g. `orders.read`, or a set of identity claims, e.g. `profile`. An administrator decides which Scopes each Client may request (*allowed Scopes*). The user then approves the request on the consent screen.

Authora remembers the approval as the user's *Consent* for the Client: the Scopes this User allowed this Client. While a Client requests only Scopes the Consent contains, the user is not asked again; a request for a further Scope shows the consent screen, and approving it adds the Scope. A Consent lasts until the user withdraws it under *Connected applications* (`/account/consents`) or an administrator revokes it (`users.manage-access`). Deleting a Scope removes it from every Consent, so a new Scope that reuses the Name is asked for again.

Each sign-in through the Client creates a *Grant*: the family of tokens the Client gets from that sign-in, so they can be revoked together. A Grant ends; the Consent stays. Revoking a Consent also revokes the Grants of that User for that Client, so the Client loses access right away instead of keeping it until its Refresh Token expires. See [Tokens, Grants and Sessions](tokens-and-sessions.md).

An API (resource server) is also registered as a Confidential Client and *serves* the Scopes it protects. It can only introspect tokens that contain a Scope it serves.

A Confidential Client can also act for itself, without any user, if an administrator allows it the Grant Type *Client Credentials*: a backend service then gets a token with the Scopes it is allowed, and its Grant has no User. Such tokens never carry a user's identity (no `openid`, no UserInfo) and never reach the admin API. See [Protect an API](../how-to/protect-an-api.md#call-the-api-from-a-backend-service).

So for applications, Authora decides **who the user is** and **which Scopes the application holds**. What the user may do inside the application, e.g. which orders they may see, is the application's decision, based on the User Id.

## Roles, Groups and Permissions: access to Authora

*Permissions* are fixed rights in Authora's admin API, such as `users.create` or `clients.manage-secrets`. Authora defines them; administrators cannot create new ones.

A *Role* bundles Permissions. A User gets Roles directly or as a member of a *Group*, and their effective Permissions are the union of all of them.

The System Role `admin` always holds every Permission and cannot be changed, deleted or given to a Group. Authora refuses to remove the last User who can sign in with it, so nobody can lock everyone out.

**Roles, Groups and Permissions are never put into tokens and never passed to applications.** They only decide what a User may do through the admin API, which checks them on every request. This avoids two problems:

- tokens that keep rights the User has since lost, and
- applications that mistake Authora's administrative Roles for their own authorization model.

If your application needs roles of its own, keep them in the application, keyed by the User Id. Releasing application roles through Authora is not supported today.

### Nobody grants more than they hold

Permissions can be delegated, e.g. a help desk Role with `users.set-password` and `users.manage-access`. Such a Permission must not become a way to more rights, so the admin API compares every change that gives out rights or acts on a User with the caller's own effective Permissions:

- a caller can only assign a Role, or add a User to a Group, if they hold every Permission it brings, and only grant a Permission to a Role that they hold themselves;
- a caller can only act on a User (set the password, deactivate, update, delete, remove a Role, …) whose effective Permissions they all hold too;
- only an administrator can assign `admin` or act on an administrator, and administrators are never restricted.

A help desk User can therefore reset the passwords of ordinary Users, but not of an administrator or of a colleague with more Permissions. A denied change returns `403` with the reason and is recorded in the audit log. The exact rules are in [Admin API](../reference/admin-api.md#nobody-grants-more-than-they-hold).

One Permission is equivalent to `admin` anyway: `clients.manage-first-party` lets its holder skip the consent screen for a Client they control, and so collect the codes of any signed-in User. Give it only to administrators.

## How the two meet: `authora.admin`

The admin API is protected by both models at once:

1. The request needs an Access Token with the Scope `authora.admin`, issued to a Client that is allowed that Scope. This is the OAuth side: *this application may manage Authora on this User's behalf.*
2. The User behind the token needs the Permission for the action. This is the RBAC side: *this User may do this.*

Neither is enough on its own. A Client allowed `authora.admin` can do nothing for a User without Permissions, and a User with Permissions cannot use an application that is not allowed `authora.admin`.

## First-party and third-party Clients

By default, a Client is *third-party*: the user is asked for consent the first time and whenever it requests a Scope the user has not approved yet. An administrator can mark a Client as *first-party*, for applications run by the same organization as Authora, like the admin interface. First-party Clients skip the consent screen. Because that is powerful, it needs a Permission of its own and locks the Client's Redirect URIs and Scopes; see [Security model](security-model.md#first-party-clients).
