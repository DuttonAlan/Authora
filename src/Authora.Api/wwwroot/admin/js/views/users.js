// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api, query } from '../api.js';
import { badge, button, details, form, formatDate, h, pageHeader, pager, panel, table } from '../dom.js';
import { auditLink, confirmed, run } from './common.js';

const pageSize = 25;

const states = ['Active', 'Inactive', 'Deactivated', 'Deleted'];

/**
 * A badge for the State of a User.
 * @param {string} state The State.
 * @returns {HTMLElement} The badge.
 */
export function stateBadge(state) {
    return badge(state, state === 'Active' ? 'ok' : state === 'Inactive' ? '' : 'bad');
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

    const searchInput = h('input', { type: 'search', value: search, placeholder: 'Email or name', 'aria-label': 'Search' });
    const stateSelect = h('select', { 'aria-label': 'State' },
        h('option', { value: '' }, 'All but deleted'),
        states.map(value => h('option', { value, selected: value === state }, value)));

    return h('div', {},
        pageHeader('Users', null,
            context.can('users.create') ? button('New User', () => context.navigate('/users?new=1'), 'primary') : null),
        context.parameters.has('new') && context.can('users.create') ? createPanel(context) : null,
        panel(null,
            h('form', {
                class: 'filters', onSubmit: event => {
                    event.preventDefault();
                    show({ search: searchInput.value.trim(), state: stateSelect.value });
                }
            },
            h('div', { class: 'field' }, h('label', { text: 'Search' }), searchInput),
            h('div', { class: 'field' }, h('label', { text: 'State' }), stateSelect),
            h('div', {}, h('button', { type: 'submit' }, 'Search'))),
            table([
                { label: 'Email', render: user => user.email },
                { label: 'Name', render: user => `${user.firstName} ${user.lastName}` },
                { label: 'State', render: user => stateBadge(user.state) }
            ], result.items, { onClick: user => context.navigate(`/users/${user.id}`), empty: 'No Users found.' }),
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
    }));
}

/**
 * The page of a User.
 * @param {object} context The context of the view.
 * @param {string} id The Id of the User.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function detail(context, id) {
    const [user, roles, groups, permissions, allRoles] = await Promise.all([
        api('GET', `/users/${id}`),
        api('GET', `/users/${id}/roles`),
        api('GET', `/users/${id}/groups`),
        api('GET', `/users/${id}/permissions`),
        context.can('roles.read') && context.can('users.assign-roles') ? api('GET', '/roles') : Promise.resolve([])
    ]);

    const deleted = user.state === 'Deleted';

    return h('div', { class: 'stack' },
        pageHeader(user.email, `${user.firstName} ${user.lastName}`, auditLink(context, 'user', user.id)),
        h('div', { class: 'grid' },
            panel('Account',
                details([
                    ['State', stateBadge(user.state)],
                    ['Failed sign-ins', String(user.failedLoginCount)],
                    ['Locked out until', formatDate(user.lockoutEndAt)],
                    ['Last sign-in', formatDate(user.lastLoginAt)],
                    ['Created', formatDate(user.createdAt)],
                    ['Modified', formatDate(user.modifiedAt)],
                    ['Id', h('code', { text: user.id })]
                ]),
                deleted ? null : accessActions(context, user)),
            !deleted && context.can('users.update') ? panel('Profile', form([
                { name: 'email', label: 'Email', type: 'email', required: true, value: user.email, maxLength: 254 },
                { name: 'firstName', label: 'First name', required: true, value: user.firstName, maxLength: 100 },
                { name: 'lastName', label: 'Last name', required: true, value: user.lastName, maxLength: 100 }
            ], 'Save', values => run(() => api('PUT', `/users/${id}`, values), 'The User was saved.', context))) : null,
            !deleted && context.can('users.set-password') ? panel('Password', form([
                {
                    name: 'password', label: 'New password', type: 'password', required: true, minLength: 12, maxLength: 128,
                    autocomplete: 'new-password', help: 'Signs the User out everywhere and revokes all their tokens.'
                }
            ], 'Set password', async (values, element) => {
                if (await run(() => api('PUT', `/users/${id}/password`, values), 'The password was set.') !== undefined) {
                    element.reset();
                }
            })) : null),
        rolesPanel(context, user, roles, allRoles, deleted),
        panel('Groups',
            table([
                { label: 'Group', render: group => context.can('groups.read') ? h('a', { href: `#/groups/${group.id}` }, group.displayName) : group.displayName },
                { label: 'Name', render: group => h('code', { text: group.name }) }
            ], groups, { empty: 'The User is in no Group.' }),
            h('p', { class: 'muted', text: 'Memberships are managed on the page of the Group.' })),
        panel('Effective Permissions',
            table([
                { label: 'Permission', render: permission => h('code', { text: permission.name }) },
                {
                    label: 'Granted by', render: permission => permission.grantedBy.map(role => role.isDirect
                        ? `${role.displayName} (direct)`
                        : `${role.displayName} (through ${role.groups.map(group => group.displayName).join(', ')})`).join('; ')
                }
            ], permissions, { empty: 'The User has no Permissions.' })));
}

function accessActions(context, user) {
    const actions = [];

    if (context.can('users.manage-access')) {
        if (user.state === 'Deactivated') {
            actions.push(button('Activate', () => run(() => api('POST', `/users/${user.id}/activate`), 'The User was activated.', context)));
        } else {
            actions.push(button('Deactivate', () => {
                if (confirmed(`Deactivate ${user.email}? The User is signed out everywhere and cannot sign in until activated again.`)) {
                    run(() => api('POST', `/users/${user.id}/deactivate`), 'The User was deactivated.', context);
                }
            }));
        }

        if (user.failedLoginCount > 0 || user.lockoutEndAt) {
            actions.push(button('Unlock', () => run(() => api('POST', `/users/${user.id}/unlock`), 'The User was unlocked.', context)));
        }

        actions.push(button('Sign out everywhere', () => {
            if (confirmed(`Revoke all Sessions and tokens of ${user.email}?`)) {
                run(() => api('POST', `/users/${user.id}/revoke-sessions`), 'All Sessions and tokens were revoked.', context);
            }
        }));
    }

    if (context.can('users.delete') && user.id !== context.me.id) {
        actions.push(button('Delete', () => {
            if (confirmed(`Delete ${user.email}? This cannot be undone.`)) {
                run(() => api('DELETE', `/users/${user.id}`), 'The User was deleted.')
                    .then(result => result !== undefined && context.navigate('/users'));
            }
        }, 'danger'));
    }

    return actions.length > 0 ? h('div', { class: 'button-row spaced' }, actions) : null;
}

function rolesPanel(context, user, roles, allRoles, deleted) {
    const canAssign = context.can('users.assign-roles') && !deleted;
    const assigned = new Set(roles.filter(role => role.isDirect).map(role => role.id));
    const available = allRoles.filter(role => !assigned.has(role.id));
    const roleSelect = h('select', { 'aria-label': 'Role' }, available.map(role => h('option', { value: role.id }, `${role.displayName} (${role.name})`)));

    return panel('Roles',
        table([
            { label: 'Role', render: role => context.can('roles.read') ? h('a', { href: `#/roles/${role.id}` }, role.displayName) : role.displayName },
            { label: 'From', render: role => [role.isDirect ? badge('direct', 'ok') : null, ...role.groups.map(group => badge(group.displayName))] },
            {
                label: '', class: 'actions-cell', render: role => canAssign && role.isDirect
                    ? button('Remove', () => run(() => api('DELETE', `/users/${user.id}/roles/${role.id}`), 'The Role was removed.', context), 'small')
                    : null
            }
        ], roles, { empty: 'The User has no Roles.' }),
        canAssign && available.length > 0
            ? h('div', { class: 'inline spaced' },
                roleSelect,
                button('Assign Role', () => run(() => api('PUT', `/users/${user.id}/roles/${roleSelect.value}`), 'The Role was assigned.', context), 'primary'))
            : null);
}
