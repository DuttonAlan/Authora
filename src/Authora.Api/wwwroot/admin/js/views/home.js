// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api, query } from '../api.js';
import { h, pageHeader, panel } from '../dom.js';
import { icon } from '../icons.js';
import { chain } from './audit.js';

const day = 24 * 60 * 60 * 1000;

// Each count is one cheap call; lists that are not paged are counted in the browser.
const areas = [
    { path: '/users', label: 'Users', icon: 'users', permission: 'users.read', count: async () => (await api('GET', `/users${query({ pageSize: 1 })}`)).total },
    { path: '/groups', label: 'Groups', icon: 'groups', permission: 'groups.read', count: async () => (await api('GET', '/groups')).length },
    { path: '/roles', label: 'Roles', icon: 'roles', permission: 'roles.read', count: async () => (await api('GET', '/roles')).length },
    { path: '/clients', label: 'Clients', icon: 'clients', permission: 'clients.read', count: async () => (await api('GET', '/clients')).length },
    { path: '/scopes', label: 'Scopes', icon: 'scopes', permission: 'scopes.read', count: async () => (await api('GET', '/scopes')).length },
    { path: '/identity-providers', label: 'Identity providers', icon: 'signIn', permission: 'identity-providers.read', count: async () => (await api('GET', '/identity-providers')).length }
];

// Failed events are what an administrator should look at: failed sign-ins, refused requests, reused tokens.
async function loadFailedEvents() {
    const from = new Date(Date.now() - day);
    const result = await api('GET', `/audit-events${query({ outcome: 'Failed', from: from.toISOString(), pageSize: 1 })}`);

    // The audit page takes local times, as its datetime-local inputs do.
    const local = new Date(from.getTime() - from.getTimezoneOffset() * 60000).toISOString().slice(0, 16);

    return { count: result.total, href: `#/audit?outcome=Failed&from=${encodeURIComponent(local)}` };
}

/**
 * The start page: what needs attention, the size of the directory and the latest events.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function show(context) {
    const visibleAreas = areas.filter(area => context.can(area.permission));
    const header = pageHeader(`${greeting()}, ${context.me.firstName}`, `Signed in as ${context.me.email}`);

    if (visibleAreas.length === 0 && !context.can('audit.read')) {
        return h('div', { class: 'stack' }, header,
            panel('No Permissions', h('p', { text: 'You can sign in to the admin interface, but have no Permissions yet. Ask an administrator to assign you a Role.' })));
    }

    // A failing call only leaves out its own part of the page.
    const [counts, attention, recent] = await Promise.all([
        Promise.allSettled(visibleAreas.map(area => area.count())),
        context.can('audit.read') ? loadFailedEvents().catch(() => null) : Promise.resolve(null),
        context.can('audit.read') ? api('GET', `/audit-events${query({ pageSize: 8 })}`).catch(() => null) : Promise.resolve(null)
    ]);

    return h('div', { class: 'stack' },
        header,
        attention !== null ? attentionPanel(attention) : null,
        h('div', { class: 'overview' },
            context.can('audit.read') ? h('section', { class: 'panel overview-activity' },
                h('div', { class: 'panel-header' },
                    h('h2', { text: 'Recent activity' }),
                    h('a', { href: '#/audit', class: 'more-link' }, 'Open audit log', icon('chevron'))),
                recent === null
                    ? h('p', { class: 'empty', text: 'The latest events could not be loaded.' })
                    : chain(context, recent.items, { compact: true, empty: 'Nothing has happened yet.' })) : null,
            visibleAreas.length > 0 ? h('section', { class: 'panel flush overview-directory' },
                h('div', { class: 'panel-header padded' }, h('h2', { text: 'Directory' })),
                h('ul', { class: 'nav-list' }, visibleAreas.map((area, index) => h('li', {},
                    h('a', { href: `#${area.path}` },
                        h('span', { class: 'nav-list-icon' }, icon(area.icon)),
                        h('span', { class: 'nav-list-label', text: area.label }),
                        h('span', { class: 'nav-list-value', text: counts[index].status === 'fulfilled' ? counts[index].value.toLocaleString() : '–' }),
                        icon('chevron', 'nav-list-chevron')))))) : null));
}

function attentionPanel({ count, href }) {
    if (count === 0) {
        return h('div', { class: 'banner ok' }, icon('shield'),
            h('div', {}, h('p', { class: 'banner-title', text: 'Nothing needs your attention.' }),
                h('p', { text: 'No failed sign-ins, refused requests or reused tokens in the last 24 hours.' })));
    }

    return h('a', { class: 'banner warn attention', href },
        icon('alert'),
        h('div', {},
            h('p', { class: 'banner-title', text: `${count.toLocaleString()} failed event${count === 1 ? '' : 's'} in the last 24 hours` }),
            h('p', { text: 'Failed sign-ins, refused requests and reused tokens. Review them in the audit log.' })),
        icon('chevron', 'attention-chevron'));
}

function greeting() {
    const hour = new Date().getHours();
    return hour < 5 ? 'Good evening' : hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
}
