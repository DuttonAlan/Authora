// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api, query } from '../api.js';
import { button, details, entityHeader, form, formatDate, h, hint, initials, pageHeader, pager, panel, replace, table } from '../dom.js';
import { icon } from '../icons.js';
import { auditLink, confirmed, emptyToNull, run } from './common.js';
import { stateBadge, userCell } from './users.js';

const pageSize = 25;

/**
 * The list of Groups.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    const groups = await api('GET', '/groups');

    return h('div', { class: 'stack' },
        pageHeader('Groups', 'Every member of a Group has all Roles of the Group.',
            context.can('groups.create') ? button('New Group', () => context.navigate('/groups?new=1'), 'primary') : null),
        context.parameters.has('new') && context.can('groups.create') ? createPanel(context) : null,
        h('section', { class: 'panel flush' }, table([
            { label: 'Group', render: group => h('span', { class: 'strong', text: group.displayName }) },
            { label: 'Name', render: group => h('code', { text: group.name }) },
            { label: 'Description', class: 'secondary', render: group => group.description ?? '' }
        ], groups, { href: group => `#/groups/${group.id}`, empty: 'No Groups exist yet.' })));
}

function createPanel(context) {
    return panel('New Group', form([
        { name: 'name', label: 'Name', required: true, maxLength: 100, help: 'Lowercase letters, digits, ".", "_" and "-", e.g. support-team.' },
        { name: 'displayName', label: 'Display name', required: true, maxLength: 100 },
        { name: 'description', label: 'Description', type: 'textarea', maxLength: 500 }
    ], 'Create Group', async values => {
        const created = await run(
            () => api('POST', '/groups', { ...values, description: emptyToNull(values.description) }),
            'The Group was created.');

        if (created) {
            context.navigate(`/groups/${created.id}`);
        }
    }, { onCancel: () => context.navigate('/groups') }));
}

/**
 * The page of a Group with its Roles and members.
 * @param {object} context The context of the view.
 * @param {string} id The Id of the Group.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function detail(context, id) {
    const page = Number(context.parameters.get('page') ?? '1');

    const [group, members, allRoles] = await Promise.all([
        api('GET', `/groups/${id}`),
        api('GET', `/groups/${id}/members${query({ page, pageSize })}`),
        context.can('roles.read') && context.can('groups.assign-roles') ? api('GET', '/roles') : Promise.resolve([])
    ]);

    return h('div', { class: 'stack' },
        entityHeader({
            kind: 'Group',
            title: group.displayName,
            subtitle: group.description,
            monogram: initials(group.displayName),
            id: group.id,
            actions: [auditLink(context, 'group', group.id)]
        }),
        h('div', { class: 'grid' },
            panel('Group', details([
                ['Name', h('code', { text: group.name })],
                ['Members', members.total.toLocaleString()],
                ['Created', formatDate(group.createdAt)],
                ['Modified', formatDate(group.modifiedAt)]
            ]),
            context.can('groups.delete')
                ? h('div', { class: 'button-row panel-actions' }, button('Delete Group', async () => {
                    if (await confirmed('Delete Group', `Delete the Group ${group.displayName}? All ${members.total} members lose its Roles.`)) {
                        run(() => api('DELETE', `/groups/${id}`), 'The Group was deleted.')
                            .then(result => result !== undefined && context.navigate('/groups'));
                    }
                }, 'destructive'))
                : null),
            context.can('groups.update') ? panel('Edit', form([
                { name: 'name', label: 'Name', required: true, value: group.name, maxLength: 100 },
                { name: 'displayName', label: 'Display name', required: true, value: group.displayName, maxLength: 100 },
                { name: 'description', label: 'Description', type: 'textarea', value: group.description ?? '', maxLength: 500 }
            ], 'Save', values => run(() => api('PUT', `/groups/${id}`, {
                ...values,
                description: values.description.trim()
            }), 'The Group was saved.', context))) : null),
        rolesPanel(context, group, allRoles),
        membersPanel(context, group, members, page));
}

function rolesPanel(context, group, allRoles) {
    const canAssign = context.can('groups.assign-roles');
    const assigned = new Set(group.roles.map(role => role.id));

    // The administrator Role can only be assigned to Users directly.
    const available = allRoles.filter(role => !assigned.has(role.id) && !(role.isSystem && role.name === 'admin'));
    const roleSelect = h('select', { 'aria-label': 'Role' }, available.map(role => h('option', { value: role.id }, `${role.displayName} (${role.name})`)));

    return panel('Roles',
        hint('Every member gets these Roles.'),
        table([
            { label: 'Role', render: role => role.displayName },
            { label: 'Name', render: role => h('code', { text: role.name }) },
            {
                label: '', class: 'actions-cell', render: role => canAssign
                    ? button('Remove', () => run(() => api('DELETE', `/groups/${group.id}/roles/${role.id}`), 'The Role was removed.', context), 'small')
                    : null
            }
        ], group.roles, { href: context.can('roles.read') ? role => `#/roles/${role.id}` : undefined, empty: 'The Group has no Roles.' }),
        canAssign && available.length > 0
            ? h('div', { class: 'inline add-row' },
                roleSelect,
                button('Assign Role', () => run(() => api('PUT', `/groups/${group.id}/roles/${roleSelect.value}`), 'The Role was assigned.', context), 'primary'))
            : null);
}

function membersPanel(context, group, members, page) {
    const canManage = context.can('groups.manage-members');

    return panel('Members',
        table([
            { label: 'User', render: userCell },
            { label: 'State', render: user => stateBadge(user.state) },
            {
                label: '', class: 'actions-cell', render: user => canManage
                    ? button('Remove', () => run(() => api('DELETE', `/groups/${group.id}/members/${user.id}`), 'The member was removed.', context), 'small')
                    : null
            }
        ], members.items, { href: context.can('users.read') ? user => `#/users/${user.id}` : undefined, empty: 'The Group has no members.' }),
        pager(page, pageSize, members.total, next => context.navigate(`/groups/${group.id}?page=${next}`)),
        canManage && context.can('users.read') ? addMember(context, group) : null);
}

// Users are found by searching, because there may be too many to list.
function addMember(context, group) {
    const searchInput = h('input', { type: 'search', placeholder: 'Find a User by email or name', 'aria-label': 'Find a User' });
    const results = h('div', { 'aria-live': 'polite' });

    const search = async event => {
        event.preventDefault();
        const found = await api('GET', `/users${query({ search: searchInput.value.trim(), pageSize: 10 })}`);

        replace(results, table([
            { label: 'User', render: userCell },
            {
                label: '', class: 'actions-cell', render: user => button('Add',
                    () => run(() => api('PUT', `/groups/${group.id}/members/${user.id}`), `${user.email} was added.`, context), 'small primary')
            }
        ], found.items, { empty: 'No Users found.' }));
    };

    return h('div', { class: 'subsection' },
        h('h3', { text: 'Add member' }),
        h('form', { class: 'inline', role: 'search', onSubmit: search },
            h('div', { class: 'search-field' }, icon('search'), searchInput),
            h('button', { type: 'submit' }, 'Search')),
        results);
}
