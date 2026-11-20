// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api, query } from '../api.js';
import { badge, copyable, h, pageHeader, pager, replace, toast } from '../dom.js';
import { icon } from '../icons.js';

const pageSize = 50;

const areas = [
    { value: '', label: 'All areas' },
    { value: 'account', label: 'Sign-in and sign-out' },
    { value: 'user', label: 'Users' },
    { value: 'group', label: 'Groups' },
    { value: 'role', label: 'Roles' },
    { value: 'client', label: 'Clients' },
    { value: 'scope', label: 'Scopes' },
    { value: 'oauth', label: 'Attacks on tokens' },
    { value: 'system', label: 'Authora' }
];

const outcomes = [
    { value: '', label: 'All' },
    { value: 'Succeeded', label: 'Succeeded' },
    { value: 'Failed', label: 'Failed' }
];

const targetTypes = ['', 'user', 'group', 'role', 'client', 'scope', 'signing-key'];

const filterNames = ['type', 'actorUserId', 'targetType', 'targetId', 'outcome', 'from', 'to'];
const moreFilterNames = ['actorUserId', 'targetType', 'targetId', 'from', 'to'];

/**
 * The audit log with filters.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    // Verifying the chain needs only 'audit.verify', so that page works without the events.
    if (!context.can('audit.read')) {
        const verification = h('div', { 'aria-live': 'polite' });

        return h('div', { class: 'stack' },
            pageHeader('Audit log', 'Check that no sealed event was changed, deleted or inserted.', verifyButton(verification)),
            verification,
            h('section', { class: 'panel' },
                h('p', { class: 'empty', text: "You need the Permission 'audit.read' to see the events." })));
    }

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
        actorUserId: h('input', { value: filters.actorUserId, placeholder: 'User Id', 'aria-label': 'Actor (User Id)' }),
        targetType: select(targetTypes.map(value => ({ value, label: value || 'All' })), filters.targetType, 'Target type'),
        targetId: h('input', { value: filters.targetId, placeholder: 'Id', 'aria-label': 'Target Id' }),
        from: h('input', { type: 'datetime-local', value: filters.from, 'aria-label': 'From' }),
        to: h('input', { type: 'datetime-local', value: filters.to, 'aria-label': 'To' })
    };

    const outcomeControl = segmented('outcome', 'Outcome', outcomes, filters.outcome);
    const activeMore = moreFilterNames.filter(name => filters[name]).length;
    const verification = h('div', { 'aria-live': 'polite' });

    const formElement = h('form', {
        class: 'audit-filters', role: 'search', onSubmit: event => {
            event.preventDefault();
            show({
                ...Object.fromEntries(Object.entries(inputs).map(([name, input]) => [name, input.value.trim()])),
                outcome: formElement.elements.outcome.value
            });
        }
    },
    h('div', { class: 'toolbar-row' },
        inputs.type,
        outcomeControl,
        h('div', { class: 'button-row toolbar-end' },
            activeMore > 0 || filters.type || filters.outcome ? h('button', { type: 'button', onClick: () => show({}) }, 'Reset') : null,
            h('button', { type: 'submit', class: 'primary' }, 'Search'))),
    h('details', { class: 'more-filters', open: activeMore > 0 },
        h('summary', {}, icon('filter'), activeMore > 0 ? `More filters (${activeMore})` : 'More filters'),
        h('div', { class: 'filters' },
            labeled('Actor (User Id)', inputs.actorUserId),
            labeled('Target type', inputs.targetType),
            labeled('Target Id', inputs.targetId),
            labeled('From', inputs.from),
            labeled('To', inputs.to))));

    // Changing the area or the outcome searches right away.
    inputs.type.addEventListener('change', () => formElement.requestSubmit());
    outcomeControl.addEventListener('change', () => formElement.requestSubmit());

    return h('div', { class: 'stack' },
        pageHeader('Audit log', 'Sign-ins and changes, newest first. Times are shown in your time zone.',
            context.can('audit.verify') ? verifyButton(verification) : null),
        verification,
        h('section', { class: 'panel' },
            formElement,
            chain(context, result.items, { empty: 'No events match the filters.' }),
            pager(page, pageSize, result.total, next => show(filters, next))));
}

function verifyButton(output) {
    const element = h('button', { type: 'button' }, icon('shield'), 'Verify chain');

    element.addEventListener('click', async () => {
        element.disabled = true;
        replace(output, h('div', { class: 'banner' }, h('span', { class: 'spinner', 'aria-hidden': 'true' }), h('p', { text: 'Verifying every sealed event…' })));

        try {
            replace(output, verificationBanner(await api('GET', '/audit-events/verification')));
        } catch (error) {
            replace(output);
            toast(error.message, 'error');
        } finally {
            element.disabled = false;
        }
    });

    return element;
}

function verificationBanner(result) {
    if (result.lastSequence === null && result.isIntact) {
        return h('div', { class: 'banner' }, icon('shield'),
            h('div', {}, h('p', { class: 'banner-title', text: 'Nothing is sealed yet.' }),
                h('p', { text: 'New events are sealed into the chain every few seconds.' })));
    }

    if (result.isIntact) {
        return h('div', { class: 'banner ok' }, icon('shield'),
            h('div', {},
                h('p', { class: 'banner-title', text: 'The chain is intact.' }),
                h('p', { text: `${result.eventsChecked.toLocaleString()} events checked, links ${result.firstSequence} to ${result.lastSequence}. Compare the last link with the latest checkpoint in the application logs.` }),
                h('p', { class: 'banner-hash' }, h('span', { text: `Link ${result.lastSequence}` }), copyable(result.lastHash, 'last hash'))));
    }

    const facts = [
        result.problem,
        result.brokenAtSequence !== null ? `First broken link: ${result.brokenAtSequence}.` : null,
        result.unsealedEvents > 0 ? `${result.unsealedEvents} events are not part of the chain.` : null
    ].filter(Boolean);

    return h('div', { class: 'banner bad', role: 'alert' }, icon('alert'),
        h('div', {},
            h('p', { class: 'banner-title', text: 'The chain is broken.' }),
            facts.map(text => h('p', { text })),
            h('p', { text: 'Events were changed, deleted or inserted in the database. Investigate before trusting the log.' })));
}

/**
 * Audit events as a chain, grouped by day: the log is sealed into a hash chain, and every event is one link.
 * @param {object} context The context of the view.
 * @param {Array<object>} events The events, newest first.
 * @param {object} [options] Options.
 * @param {string} [options.empty] The text when there are no events.
 * @param {boolean} [options.compact] Leaves out IP addresses and details.
 * @returns {HTMLElement} The chain.
 */
export function chain(context, events, { empty = 'No events.', compact = false } = {}) {
    if (events.length === 0) {
        return h('p', { class: 'empty', text: empty });
    }

    const days = new Map();

    for (const event of events) {
        const day = dayLabel(new Date(event.occurredAt));
        days.set(day, [...(days.get(day) ?? []), event]);
    }

    return h('div', { class: compact ? 'chain compact' : 'chain' }, [...days.entries()].map(([day, items]) => h('section', { class: 'chain-day' },
        h('h3', { class: 'chain-date', text: day }),
        h('ol', { class: 'chain-links' }, items.map(event => link(context, event, compact))))));
}

function link(context, event, compact) {
    const failed = event.outcome !== 'Succeeded';
    const time = new Date(event.occurredAt);

    return h('li', { class: failed ? 'chain-link failed' : 'chain-link' },
        h('span', { class: 'chain-node', 'aria-hidden': 'true' }),
        h('div', { class: 'chain-body' },
            h('div', { class: 'chain-head' },
                h('code', { class: 'event-type', text: event.type }),
                failed ? badge(event.outcome, 'bad') : h('span', { class: 'visually-hidden', text: event.outcome }),
                h('time', { class: 'chain-time', datetime: time.toISOString(), title: time.toLocaleString(), text: time.toLocaleTimeString() })),
            h('div', { class: 'chain-meta' },
                actor(context, event),
                target(context, event),
                !compact && event.ipAddress ? h('span', { class: 'mono', text: event.ipAddress }) : null),
            !compact && event.details ? h('p', { class: 'chain-details', text: event.details }) : null));
}

function dayLabel(date) {
    const today = new Date();
    const yesterday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);

    if (date.toDateString() === today.toDateString()) {
        return 'Today';
    }

    if (date.toDateString() === yesterday.toDateString()) {
        return 'Yesterday';
    }

    return date.toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
}

function actor(context, event) {
    const parts = [];

    if (event.actorUserId) {
        const label = event.actorEmail ?? event.actorUserId;
        parts.push(context.can('users.read') ? h('a', { href: `#/users/${event.actorUserId}` }, label) : h('span', { text: label }));
    }

    if (event.actorClientId) {
        parts.push(h('span', { class: 'muted', text: `via ${event.actorClientName ?? event.actorClientId}` }));
    }

    return h('span', { class: 'chain-actor' },
        parts.length > 0 ? parts : h('span', { class: 'muted', text: event.ipAddress ? 'Not signed in' : 'Authora' }));
}

const targetPages = { user: 'users', group: 'groups', role: 'roles', client: 'clients', scope: 'scopes' };
const targetPermissions = { user: 'users.read', group: 'groups.read', role: 'roles.read', client: 'clients.read', scope: 'scopes.read' };

function target(context, event) {
    if (!event.targetType) {
        return null;
    }

    const label = `${event.targetType} ${shortId(event.targetId)}`;
    const page = targetPages[event.targetType];

    return page && context.can(targetPermissions[event.targetType])
        ? h('a', { href: `#/${page}/${event.targetId}`, class: 'mono', title: event.targetId }, '→ ', label)
        : h('span', { class: 'mono', title: event.targetId ?? null, text: `→ ${label}` });
}

// Ids are long; the start is enough to tell them apart on screen, the tooltip has the full value.
function shortId(id) {
    return id && /^[0-9a-f-]{36}$/i.test(id) ? `${id.slice(0, 8)}…` : (id ?? '');
}

function segmented(name, label, options, value) {
    return h('div', { class: 'segmented', role: 'radiogroup', 'aria-label': label },
        options.map(option => h('label', {},
            h('input', { type: 'radio', name, value: option.value, checked: option.value === value }),
            h('span', { text: option.label }))));
}

function select(options, value, label) {
    return h('select', { 'aria-label': label }, options.map(option => h('option', { value: option.value, selected: option.value === value }, option.label)));
}

function labeled(label, input) {
    const id = `filter-${Math.random().toString(36).slice(2, 8)}`;
    input.id = id;

    return h('div', { class: 'field' }, h('label', { for: id, text: label }), input);
}

// datetime-local has no time zone; it is the local time of the browser.
function toIso(value) {
    return value ? new Date(value).toISOString() : null;
}
