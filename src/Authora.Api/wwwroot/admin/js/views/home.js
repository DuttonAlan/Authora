// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { h, pageHeader, panel } from '../dom.js';

const areas = [
    { path: '/users', label: 'Users', permission: 'users.read', text: 'Create Users, change their access, passwords and Roles.' },
    { path: '/groups', label: 'Groups', permission: 'groups.read', text: 'Organize Users and give all members the same Roles.' },
    { path: '/roles', label: 'Roles', permission: 'roles.read', text: 'Bundle Permissions into reusable Roles.' },
    { path: '/clients', label: 'Clients', permission: 'clients.read', text: 'Register applications, their Redirect URIs, Scopes and Secrets.' },
    { path: '/scopes', label: 'Scopes', permission: 'scopes.read', text: 'Define what applications may request.' },
    { path: '/audit', label: 'Audit log', permission: 'audit.read', text: 'Trace sign-ins and changes.' }
];

/**
 * The start page with the areas the User may open.
 * @param {object} context The context of the view.
 * @returns {HTMLElement} The page.
 */
export function show(context) {
    const visible = areas.filter(area => context.can(area.permission));

    return h('div', {},
        pageHeader(`Welcome, ${context.me.firstName}`, `Signed in as ${context.me.email}`),
        visible.length === 0
            ? panel('No Permissions', h('p', { text: 'You can sign in to the admin interface, but have no Permissions yet. Ask an administrator to assign you a Role.' }))
            : h('div', { class: 'grid' }, visible.map(area => panel(null,
                h('h2', {}, h('a', { href: `#${area.path}` }, area.label)),
                h('p', { class: 'muted', text: area.text })))));
}
