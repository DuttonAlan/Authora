// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api, query } from '../api.js';
import { badge, formatDate, h, pageHeader, pager, panel, table } from '../dom.js';

const pageSize = 50;

const areas = [
    { value: '', label: 'All' },
    { value: 'account', label: 'Sign-in and sign-out' },
    { value: 'user', label: 'Users' },
    { value: 'group', label: 'Groups' },
    { value: 'role', label: 'Roles' },
    { value: 'client', label: 'Clients' },
    { value: 'scope', label: 'Scopes' },
    { value: 'oauth', label: 'Attacks on tokens' },
    { value: 'system', label: 'Authora' }
];

const targetTypes = ['', 'user', 'group', 'role', 'client', 'scope', 'signing-key'];

const filterNames = ['type', 'actorUserId', 'targetType', 'targetId', 'outcome', 'from', 'to'];

/**
 * The audit log with filters.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    const filters = Object.fromEntries(filterNames.map(name => [name, context.parameters.get(name) ?? '']));
    const page = Number(context.parameters.get('page') ?? '1');

    const result = await api('GET', `/audit-events${query({
        ...filters,
        from: toIso(filters.from),
        to: toIso(filters.to),
        page,
        pageSize
    })}`);

    const show = (values, nextPage = 1) => {
        const parameters = new URLSearchParams();

        for (const name of filterNames) {
            if (values[name]) {
                parameters.set(name, values[name]);
            }
        }

        if (nextPage > 1) {
            parameters.set('page', String(nextPage));
        }

        context.navigate(`/audit${parameters.size > 0 ? `?${parameters}` : ''}`);
    };

    const inputs = {
        type: select(areas, filters.type, 'Area'),
        outcome: select([{ value: '', label: 'All' }, { value: 'Succeeded', label: 'Succeeded' }, { value: 'Failed', label: 'Failed' }], filters.outcome, 'Outcome'),
        actorUserId: h('input', { value: filters.actorUserId, placeholder: 'User Id', 'aria-label': 'Actor' }),
        targetType: select(targetTypes.map(value => ({ value, label: value || 'All' })), filters.targetType, 'Target type'),
        targetId: h('input', { value: filters.targetId, placeholder: 'Id', 'aria-label': 'Target Id' }),
        from: h('input', { type: 'datetime-local', value: filters.from, 'aria-label': 'From' }),
        to: h('input', { type: 'datetime-local', value: filters.to, 'aria-label': 'To' })
    };

    return h('div', {},
        pageHeader('Audit log', 'Sign-ins and changes, newest first. Times are shown in your time zone.'),
        panel(null,
            h('form', {
                class: 'filters', onSubmit: event => {
                    event.preventDefault();
                    show(Object.fromEntries(Object.entries(inputs).map(([name, input]) => [name, input.value.trim()])));
                }
            },
            labeled('Area', inputs.type),
            labeled('Outcome', inputs.outcome),
            labeled('Actor (User Id)', inputs.actorUserId),
            labeled('Target type', inputs.targetType),
            labeled('Target Id', inputs.targetId),
            labeled('From', inputs.from),
            labeled('To', inputs.to),
            h('div', { class: 'button-row' },
                h('button', { type: 'submit', class: 'primary' }, 'Search'),
                h('button', { type: 'button', onClick: () => show({}) }, 'Reset'))),
            table([
                { label: 'Time', render: event => formatDate(event.occurredAt) },
                { label: 'Event', render: event => h('code', { text: event.type }) },
                { label: 'Outcome', render: event => badge(event.outcome, event.outcome === 'Succeeded' ? 'ok' : 'bad') },
                { label: 'Actor', render: event => actor(context, event) },
                { label: 'IP address', render: event => event.ipAddress ?? '–' },
                { label: 'Target', render: event => target(context, event) },
                { label: 'Details', render: event => event.details ?? '' }
            ], result.items, { empty: 'No events match the filters.' }),
            pager(page, pageSize, result.total, next => show(filters, next))));
}

function actor(context, event) {
    const parts = [];

    if (event.actorUserId) {
        const label = event.actorEmail ?? event.actorUserId;
        parts.push(context.can('users.read') ? h('a', { href: `#/users/${event.actorUserId}` }, label) : label);
    }

    if (event.actorClientId) {
        parts.push(h('div', { class: 'muted', text: `via ${event.actorClientName ?? event.actorClientId}` }));
    }

    return parts.length > 0 ? parts : h('span', { class: 'muted', text: event.ipAddress ? 'not signed in' : 'Authora' });
}

const targetPages = { user: 'users', group: 'groups', role: 'roles', client: 'clients', scope: 'scopes' };
const targetPermissions = { user: 'users.read', group: 'groups.read', role: 'roles.read', client: 'clients.read', scope: 'scopes.read' };

function target(context, event) {
    if (!event.targetType) {
        return '–';
    }

    const label = `${event.targetType} ${event.targetId}`;
    const page = targetPages[event.targetType];

    return page && context.can(targetPermissions[event.targetType])
        ? h('a', { href: `#/${page}/${event.targetId}`, class: 'mono' }, label)
        : h('span', { class: 'mono', text: label });
}

function select(options, value, label) {
    return h('select', { 'aria-label': label }, options.map(option => h('option', { value: option.value, selected: option.value === value }, option.label)));
}

function labeled(label, input) {
    return h('div', { class: 'field' }, h('label', { text: label }), input);
}

// datetime-local has no time zone; it is the local time of the browser.
function toIso(value) {
    return value ? new Date(value).toISOString() : null;
}
