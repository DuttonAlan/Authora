// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api, query } from '../api.js';
import { badge, button, details, entityHeader, form, formatDate, h, hint, initials, pageHeader, pager, panel, relativeTime, table } from '../dom.js';
import { icon } from '../icons.js';
import { auditLink, confirmed, run } from './common.js';

const pageSize = 25;

const states = ['Active', 'Inactive', 'Deactivated', 'Deleted'];

/**
 * A badge for the State of a User.
 * @param {string} state The State.
 * @returns {HTMLElement} The badge.
 */
export function stateBadge(state) {
    return badge(state, state === 'Active' ? 'ok' : state === 'Inactive' ? 'neutral' : 'bad');
}

/**
 * A User's name with their monogram, for lists.
 * @param {object} user The User.
 * @returns {HTMLElement} The cell content.
 */
export function userCell(user) {
    return h('span', { class: 'person' },
        h('span', { class: 'avatar', 'aria-hidden': 'true', text: initials(user.firstName, user.lastName).toUpperCase() }),
        h('span', { class: 'person-text' },
            h('span', { class: 'person-name', text: `${user.firstName} ${user.lastName}` }),
            h('span', { class: 'person-email', text: user.email })));
}

/**
 * The list of Users with search, filter and paging.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    const search = context.parameters.get('search') ?? '';
    const state = context.parameters.get('state') ?? '';
    const page = Number(context.parameters.get('page') ?? '1');

    const result = await api('GET', `/users${query({ search, state, page, pageSize })}`);

    const show = changes => {
        const parameters = new URLSearchParams({ search, state, page: '1', ...changes });

        for (const [key, value] of [...parameters]) {
            if (value === '' || (key === 'page' && value === '1')) {
                parameters.delete(key);
            }
        }

        context.navigate(`/users${parameters.size > 0 ? `?${parameters}` : ''}`);
    };

    const searchInput = h('input', { type: 'search', value: search, placeholder: 'Search by email or name', 'aria-label': 'Search' });
    const stateSelect = h('select', { 'aria-label': 'State', onChange: () => show({ search: searchInput.value.trim(), state: stateSelect.value }) },
        h('option', { value: '' }, 'All but deleted'),
        states.map(value => h('option', { value, selected: value === state }, value)));

    return h('div', { class: 'stack' },
        pageHeader('Users', 'People who sign in with Authora.',
            context.can('users.create') ? button('New User', () => context.navigate('/users?new=1'), 'primary') : null),
        context.parameters.has('new') && context.can('users.create') ? createPanel(context) : null,
        h('section', { class: 'panel flush' },
            h('form', {
                class: 'toolbar-row', role: 'search', onSubmit: event => {
                    event.preventDefault();
                    show({ search: searchInput.value.trim(), state: stateSelect.value });
                }
            },
            h('div', { class: 'search-field' }, icon('search'), searchInput),
            stateSelect,
            h('button', { type: 'submit' }, 'Search')),
            table([
                { label: 'User', render: userCell },
                { label: 'State', render: user => stateBadge(user.state) }
            ], result.items, { href: user => `#/users/${user.id}`, empty: search || state ? 'No Users match the search.' : 'No Users exist yet.' }),
            pager(page, pageSize, result.total, next => show({ page: String(next) }))));
}

function createPanel(context) {
    return panel('New User', form([
        { name: 'email', label: 'Email', type: 'email', required: true, maxLength: 254 },
        { name: 'firstName', label: 'First name', required: true, maxLength: 100 },
        { name: 'lastName', label: 'Last name', required: true, maxLength: 100 },
        {
            name: 'password', label: 'Initial password', type: 'password', required: true, minLength: 12, maxLength: 128,
            autocomplete: 'new-password', help: '12 to 128 characters. Give it to the User through a secure channel.'
        }
    ], 'Create User', async values => {
        const created = await run(() => api('POST', '/users', values), 'The User was created.');

        if (created) {
            context.navigate(`/users/${created.id}`);
        }
    }, { onCancel: () => context.navigate('/users') }));
}

/**
 * The page of a User.
 * @param {object} context The context of the view.
 * @param {string} id The Id of the User.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function detail(context, id) {
    const [user, roles, groups, permissions, consents, allRoles] = await Promise.all([
        api('GET', `/users/${id}`),
        api('GET', `/users/${id}/roles`),
        api('GET', `/users/${id}/groups`),
        api('GET', `/users/${id}/permissions`),
        api('GET', `/users/${id}/consents`),
        context.can('roles.read') && context.can('users.assign-roles') ? api('GET', '/roles') : Promise.resolve([])
    ]);

    const deleted = user.state === 'Deleted';
    const locked = user.lockoutEndAt && new Date(user.lockoutEndAt) > new Date();

    return h('div', { class: 'stack' },
        entityHeader({
            kind: 'User',
            title: `${user.firstName} ${user.lastName}`,
            subtitle: user.email,
            monogram: initials(user.firstName, user.lastName),
            status: h('span', { class: 'badge-row' }, stateBadge(user.state), locked ? badge('Locked out', 'warn') : null),
            id: user.id,
            actions: [auditLink(context, 'user', user.id)]
        }),
        h('div', { class: 'grid' },
            panel('Account',
                details([
                    ['Email', user.emailVerified
                        ? h('span', { class: 'badge-row' }, badge('Verified', 'ok'), formatDate(user.emailVerifiedAt))
                        : badge('Not verified', 'warn')],
                    ['Last sign-in', relativeTime(user.lastLoginAt)],
                    ['Failed sign-ins', String(user.failedLoginCount)],
                    ['Locked out until', formatDate(user.lockoutEndAt)],
                    ['Created', formatDate(user.createdAt)],
                    ['Modified', formatDate(user.modifiedAt)]
                ]),
                deleted ? null : accessActions(context, user)),
            !deleted && context.can('users.update') ? panel('Profile', form([
                { name: 'email', label: 'Email', type: 'email', required: true, value: user.email, maxLength: 254 },
                { name: 'firstName', label: 'First name', required: true, value: user.firstName, maxLength: 100 },
                { name: 'lastName', label: 'Last name', required: true, value: user.lastName, maxLength: 100 }
            ], 'Save', values => run(() => api('PUT', `/users/${id}`, values), 'The User was saved.', context)),
            emailVerificationActions(context, user)) : null),
        rolesPanel(context, user, roles, allRoles, deleted),
        panel('Groups',
            hint('Memberships are managed on the page of the Group.'),
            table([
                { label: 'Group', render: group => group.displayName },
                { label: 'Name', render: group => h('code', { text: group.name }) }
            ], groups, { href: context.can('groups.read') ? group => `#/groups/${group.id}` : undefined, empty: 'The User is in no Group.' })),
        panel('Effective Permissions',
            hint('Everything the User may do, and the Roles that grant it.'),
            table([
                { label: 'Permission', render: permission => h('code', { class: 'token', text: permission.name }) },
                {
                    label: 'Granted by', render: permission => h('span', { class: 'badge-row' }, permission.grantedBy.map(role => role.isDirect
                        ? badge(`${role.displayName} · direct`)
                        : badge(`${role.displayName} · through ${role.groups.map(group => group.displayName).join(', ')}`)))
                }
            ], permissions, { empty: 'The User has no Permissions.' })),
        consentsPanel(context, user, consents),
        !deleted && context.can('users.set-password') ? panel('Password', form([
            {
                name: 'password', label: 'New password', type: 'password', required: true, minLength: 12, maxLength: 128,
                autocomplete: 'new-password', help: 'Signs the User out everywhere and revokes all their tokens.'
            }
        ], 'Set password', async (values, element) => {
            if (await run(() => api('PUT', `/users/${id}/password`, values), 'The password was set.') !== undefined) {
                element.reset();
            }
        }), passwordResetActions(user)) : null);
}

function passwordResetActions(user) {
    if (user.state === 'Deactivated') {
        return null;
    }

    return [
        hint('Or let the User choose a new password themselves: a link goes to their email address. Their current password keeps working until they use it.'),
        h('div', { class: 'button-row panel-actions' }, button('Send reset link', async () => {
            if (await confirmed('Send reset link', `Send a password reset link to ${user.email}? Earlier links stop working.`)) {
                run(() => api('POST', `/users/${user.id}/password-reset`), 'The reset link was sent.');
            }
        }))
    ];
}

function emailVerificationActions(context, user) {
    if (user.emailVerified) {
        return null;
    }

    const actions = [];

    if (user.state !== 'Deactivated') {
        actions.push(button('Send verification link', async () => {
            if (await confirmed('Send verification link', `Send a link to confirm ${user.email}? Earlier links stop working.`)) {
                run(() => api('POST', `/users/${user.id}/email-verification`), 'The verification link was sent.');
            }
        }));
    }

    actions.push(button('Mark as verified', async () => {
        if (await confirmed('Mark as verified', `Mark ${user.email} as verified? Applications may then trust that it belongs to this User. Only do this if you know it does.`)) {
            run(() => api('POST', `/users/${user.id}/email-verified`), 'The email address was marked as verified.', context);
        }
    }));

    return [
        hint('The email address is not verified. Changing it resets the verification and sends a new link.'),
        h('div', { class: 'button-row panel-actions' }, actions)
    ];
}

function accessActions(context, user) {
    const actions = [];

    if (context.can('users.manage-access')) {
        if (user.state === 'Deactivated') {
            actions.push(button('Activate', () => run(() => api('POST', `/users/${user.id}/activate`), 'The User was activated.', context)));
        } else {
            actions.push(button('Deactivate', async () => {
                if (await confirmed('Deactivate', `Deactivate ${user.email}? The User is signed out everywhere and cannot sign in until activated again.`)) {
                    run(() => api('POST', `/users/${user.id}/deactivate`), 'The User was deactivated.', context);
                }
            }));
        }

        if (user.failedLoginCount > 0 || user.lockoutEndAt) {
            actions.push(button('Unlock', () => run(() => api('POST', `/users/${user.id}/unlock`), 'The User was unlocked.', context)));
        }

        actions.push(button('Sign out everywhere', async () => {
            if (await confirmed('Sign out everywhere',`Revoke all Sessions and tokens of ${user.email}? The User has to sign in again in every application.`)) {
                run(() => api('POST', `/users/${user.id}/revoke-sessions`), 'All Sessions and tokens were revoked.', context);
            }
        }));
    }

    if (context.can('users.delete') && user.id !== context.me.id) {
        actions.push(button('Delete', async () => {
            if (await confirmed('Delete', `Delete ${user.email}? This cannot be undone.`)) {
                run(() => api('DELETE', `/users/${user.id}`), 'The User was deleted.')
                    .then(result => result !== undefined && context.navigate('/users'));
            }
        }, 'destructive'));
    }

    return actions.length > 0 ? h('div', { class: 'button-row panel-actions' }, actions) : null;
}

function rolesPanel(context, user, roles, allRoles, deleted) {
    const canAssign = context.can('users.assign-roles') && !deleted;
    const assigned = new Set(roles.filter(role => role.isDirect).map(role => role.id));
    const available = allRoles.filter(role => !assigned.has(role.id));
    const roleSelect = h('select', { 'aria-label': 'Role' }, available.map(role => h('option', { value: role.id }, `${role.displayName} (${role.name})`)));

    return panel('Roles',
        table([
            { label: 'Role', render: role => role.displayName },
            { label: 'From', render: role => h('span', { class: 'badge-row' }, role.isDirect ? badge('Direct', 'ok') : null, role.groups.map(group => badge(group.displayName))) },
            {
                label: '', class: 'actions-cell', render: role => canAssign && role.isDirect
                    ? button('Remove', () => run(() => api('DELETE', `/users/${user.id}/roles/${role.id}`), 'The Role was removed.', context), 'small')
                    : null
            }
        ], roles, { href: context.can('roles.read') ? role => `#/roles/${role.id}` : undefined, empty: 'The User has no Roles.' }),
        canAssign && available.length > 0
            ? h('div', { class: 'inline add-row' },
                roleSelect,
                button('Assign Role', () => run(() => api('PUT', `/users/${user.id}/roles/${roleSelect.value}`), 'The Role was assigned.', context), 'primary'))
            : null);
}

function consentsPanel(context, user, consents) {
    const canRevoke = context.can('users.manage-access');

    return panel('Consents',
        hint('The applications the User allowed to access their account. Revoking a Consent also revokes the application\'s tokens for the User, who is asked again on the next sign-in.'),
        table([
            { label: 'Client', render: consent => consent.clientName },
            { label: 'Scopes', render: consent => h('span', { class: 'badge-row' }, consent.scopes.map(scope => h('code', { class: 'token', text: scope.name }))) },
            { label: 'Allowed', render: consent => formatDate(consent.createdAt) },
            {
                label: '', class: 'actions-cell', render: consent => canRevoke
                    ? button('Revoke', async () => {
                        if (await confirmed('Revoke Consent', `Revoke the Consent of ${user.email} for ${consent.clientName}? Its tokens for the User stop working.`)) {
                            run(() => api('DELETE', `/users/${user.id}/consents/${consent.clientId}`), 'The Consent was revoked.', context);
                        }
                    }, 'small')
                    : null
            }
        ], consents, { href: context.can('clients.read') ? consent => `#/clients/${consent.clientId}` : undefined, empty: 'The User has not allowed any application.' }));
}
