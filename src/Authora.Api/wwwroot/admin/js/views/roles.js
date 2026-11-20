// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api } from '../api.js';
import { badge, button, details, entityHeader, form, formatDate, h, hint, initials, pageHeader, panel, table } from '../dom.js';
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

    return h('div', { class: 'stack' },
        pageHeader('Roles', 'Roles bundle Permissions. Users get them directly or through Groups.',
            context.can('roles.create') ? button('New Role', () => context.navigate('/roles?new=1'), 'primary') : null),
        context.parameters.has('new') && context.can('roles.create') ? createPanel(context) : null,
        h('section', { class: 'panel flush' }, table([
            { label: 'Role', render: role => h('span', { class: 'badge-row' }, h('span', { class: 'strong', text: role.displayName }), role.isSystem ? badge('System') : null) },
            { label: 'Name', render: role => h('code', { text: role.name }) },
            { label: 'Description', class: 'secondary', render: role => role.description ?? '' }
        ], roles, { href: role => `#/roles/${role.id}`, empty: 'No Roles exist yet.' })));
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
    }, { onCancel: () => context.navigate('/roles') }));
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
        entityHeader({
            kind: role.isSystem ? 'System Role' : 'Role',
            title: role.displayName,
            subtitle: role.description,
            monogram: initials(role.displayName),
            id: role.id,
            actions: [auditLink(context, 'role', role.id)]
        }),
        h('div', { class: 'grid' },
            panel('Role', details([
                ['Name', h('code', { text: role.name })],
                ['System Role', role.isSystem ? 'Yes, it cannot be deleted' : 'No'],
                ['Permissions', isAdministrator ? 'All' : role.permissions.length.toLocaleString()],
                ['Created', formatDate(role.createdAt)],
                ['Modified', formatDate(role.modifiedAt)]
            ]),
            context.can('roles.delete') && !role.isSystem
                ? h('div', { class: 'button-row panel-actions' }, button('Delete Role', async () => {
                    if (await confirmed('Delete Role', `Delete the Role ${role.displayName}? All Users and Groups lose it.`)) {
                        run(() => api('DELETE', `/roles/${id}`), 'The Role was deleted.')
                            .then(result => result !== undefined && context.navigate('/roles'));
                    }
                }, 'destructive'))
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

// Permission names are "<area>.<action>"; grouping them by area keeps the long list scannable.
function byArea(permissions) {
    const areas = new Map();

    for (const permission of permissions) {
        const area = permission.name.split('.')[0];
        areas.set(area, [...(areas.get(area) ?? []), permission]);
    }

    return [...areas.entries()];
}

function permissionsPanel(context, role, permissions, granted, isAdministrator) {
    const canGrant = context.can('roles.grant-permissions') && !isAdministrator;

    if (permissions === null || !canGrant) {
        return panel('Permissions',
            isAdministrator ? hint('The administrator Role always has all Permissions.') : null,
            role.permissions.length === 0
                ? h('p', { class: 'empty', text: 'The Role has no Permissions.' })
                : h('div', { class: 'permission-areas' }, byArea(role.permissions).map(([area, items]) => h('div', { class: 'permission-area' },
                    h('h3', { text: area }),
                    h('div', { class: 'badge-row' }, items.map(permission => h('code', { class: 'token', text: permission.name })))))));
    }

    return panel('Permissions',
        hint(`${granted.size} of ${permissions.length} granted. Changes take effect on the next request of the Users.`),
        h('div', { class: 'permission-areas' }, byArea(permissions).map(([area, items]) => h('fieldset', { class: 'permission-area' },
            h('legend', { text: area }),
            h('div', { class: 'checks' }, items.map(permission => h('label', { class: 'check' },
                h('input', {
                    type: 'checkbox',
                    checked: granted.has(permission.id),
                    onChange: event => run(
                        () => api(event.target.checked ? 'PUT' : 'DELETE', `/roles/${role.id}/permissions/${permission.id}`),
                        event.target.checked ? `${permission.name} was granted.` : `${permission.name} was revoked.`,
                        context)
                }),
                h('code', { text: permission.name }))))))));
}
