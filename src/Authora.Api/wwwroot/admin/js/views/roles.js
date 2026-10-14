// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api } from '../api.js';
import { badge, button, details, form, formatDate, h, pageHeader, panel, table } from '../dom.js';
import { auditLink, confirmed, emptyToNull, run } from './common.js';

// Role.AdministratorName: the System Role that always has all Permissions.
const administratorName = 'admin';

/**
 * The list of Roles.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    const roles = await api('GET', '/roles');

    return h('div', {},
        pageHeader('Roles', 'Roles bundle Permissions. Users get them directly or through Groups.',
            context.can('roles.create') ? button('New Role', () => context.navigate('/roles?new=1'), 'primary') : null),
        context.parameters.has('new') && context.can('roles.create') ? createPanel(context) : null,
        panel(null, table([
            { label: 'Role', render: role => role.displayName },
            { label: 'Name', render: role => h('code', { text: role.name }) },
            { label: 'Description', render: role => role.description ?? '' },
            { label: '', render: role => role.isSystem ? badge('System') : null }
        ], roles, { onClick: role => context.navigate(`/roles/${role.id}`), empty: 'No Roles exist.' })));
}

function createPanel(context) {
    return panel('New Role', form([
        { name: 'name', label: 'Name', required: true, maxLength: 100, help: 'Lowercase letters, digits, ".", "_" and "-", e.g. support-agent.' },
        { name: 'displayName', label: 'Display name', required: true, maxLength: 100 },
        { name: 'description', label: 'Description', type: 'textarea', maxLength: 500 }
    ], 'Create Role', async values => {
        const created = await run(
            () => api('POST', '/roles', { ...values, description: emptyToNull(values.description) }),
            'The Role was created.');

        if (created) {
            context.navigate(`/roles/${created.id}`);
        }
    }));
}

/**
 * The page of a Role with its Permissions.
 * @param {object} context The context of the view.
 * @param {string} id The Id of the Role.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function detail(context, id) {
    const [role, permissions] = await Promise.all([
        api('GET', `/roles/${id}`),
        context.can('permissions.read') ? api('GET', '/permissions') : Promise.resolve(null)
    ]);

    const isAdministrator = role.isSystem && role.name === administratorName;
    const granted = new Set(role.permissions.map(permission => permission.id));

    return h('div', { class: 'stack' },
        pageHeader(role.displayName, role.description, auditLink(context, 'role', role.id)),
        h('div', { class: 'grid' },
            panel('Role', details([
                ['Name', h('code', { text: role.name })],
                ['System Role', role.isSystem ? 'Yes, it cannot be deleted' : 'No'],
                ['Created', formatDate(role.createdAt)],
                ['Modified', formatDate(role.modifiedAt)],
                ['Id', h('code', { text: role.id })]
            ]),
            context.can('roles.delete') && !role.isSystem
                ? h('div', { class: 'button-row spaced' }, button('Delete Role', () => {
                    if (confirmed(`Delete the Role ${role.displayName}? All Users and Groups lose it.`)) {
                        run(() => api('DELETE', `/roles/${id}`), 'The Role was deleted.')
                            .then(result => result !== undefined && context.navigate('/roles'));
                    }
                }, 'danger'))
                : null),
            context.can('roles.update') ? panel('Edit', form([
                { name: 'name', label: 'Name', required: true, value: role.name, disabled: role.isSystem, maxLength: 100 },
                { name: 'displayName', label: 'Display name', required: true, value: role.displayName, maxLength: 100 },
                { name: 'description', label: 'Description', type: 'textarea', value: role.description ?? '', maxLength: 500 }
            ], 'Save', values => run(() => api('PUT', `/roles/${id}`, {
                name: role.isSystem ? null : values.name,
                displayName: values.displayName,
                // An empty Description removes it.
                description: values.description.trim()
            }), 'The Role was saved.', context))) : null),
        permissionsPanel(context, role, permissions, granted, isAdministrator));
}

function permissionsPanel(context, role, permissions, granted, isAdministrator) {
    const canGrant = context.can('roles.grant-permissions') && !isAdministrator;

    if (permissions === null || !canGrant) {
        return panel('Permissions',
            isAdministrator ? h('p', { class: 'muted', text: 'The administrator Role always has all Permissions.' }) : null,
            table([{ label: 'Permission', render: permission => h('code', { text: permission.name }) }],
                role.permissions, { empty: 'The Role has no Permissions.' }));
    }

    return panel('Permissions',
        h('p', { class: 'muted', text: 'Changes take effect on the next request of the Users.' }),
        h('div', { class: 'checks' }, permissions.map(permission => h('label', {},
            h('input', {
                type: 'checkbox',
                checked: granted.has(permission.id),
                onChange: event => run(
                    () => api(event.target.checked ? 'PUT' : 'DELETE', `/roles/${role.id}/permissions/${permission.id}`),
                    event.target.checked ? `${permission.name} was granted.` : `${permission.name} was revoked.`,
                    context)
            }),
            h('code', { text: permission.name })))));
}
