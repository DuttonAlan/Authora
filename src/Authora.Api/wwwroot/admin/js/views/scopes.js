// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api } from '../api.js';
import { badge, button, details, entityHeader, form, formatDate, h, initials, pageHeader, panel, table } from '../dom.js';
import { auditLink, confirmed, emptyToNull, run } from './common.js';

/**
 * The list of Scopes.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    const scopes = await api('GET', '/scopes');

    return h('div', { class: 'stack' },
        pageHeader('Scopes', 'Clients may only request the Scopes an administrator allowed them.',
            context.can('scopes.create') ? button('New Scope', () => context.navigate('/scopes?new=1'), 'primary') : null),
        context.parameters.has('new') && context.can('scopes.create') ? createPanel(context) : null,
        h('section', { class: 'panel flush' }, table([
            { label: 'Scope', render: scope => h('span', { class: 'badge-row' }, h('code', { class: 'token', text: scope.name }), scope.isSystem ? badge('System') : null) },
            { label: 'Display name', render: scope => scope.displayName },
            { label: 'Description', class: 'secondary', render: scope => scope.description ?? '' }
        ], scopes, { href: scope => `#/scopes/${scope.id}`, empty: 'No Scopes exist yet.' })));
}

function createPanel(context) {
    return panel('New Scope', form([
        { name: 'name', label: 'Name', required: true, maxLength: 100, help: 'The value Clients request, e.g. orders.read. It cannot be changed later.' },
        { name: 'displayName', label: 'Display name', required: true, maxLength: 100, help: 'Shown to Users on the consent screen.' },
        { name: 'description', label: 'Description', type: 'textarea', maxLength: 500, help: 'Shown to Users on the consent screen.' }
    ], 'Create Scope', async values => {
        const created = await run(
            () => api('POST', '/scopes', { ...values, description: emptyToNull(values.description) }),
            'The Scope was created.');

        if (created) {
            context.navigate(`/scopes/${created.id}`);
        }
    }, { onCancel: () => context.navigate('/scopes') }));
}

/**
 * The page of a Scope.
 * @param {object} context The context of the view.
 * @param {string} id The Id of the Scope.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function detail(context, id) {
    const scope = await api('GET', `/scopes/${id}`);

    return h('div', { class: 'stack' },
        entityHeader({
            kind: scope.isSystem ? 'System Scope' : 'Scope',
            title: scope.displayName,
            subtitle: scope.description,
            monogram: initials(scope.displayName),
            id: scope.id,
            actions: [auditLink(context, 'scope', scope.id)]
        }),
        h('div', { class: 'grid' },
            panel('Scope', details([
                ['Name', h('code', { class: 'token', text: scope.name })],
                ['System Scope', scope.isSystem ? 'Yes, it cannot be deleted' : 'No'],
                ['Created', formatDate(scope.createdAt)],
                ['Modified', formatDate(scope.modifiedAt)]
            ]),
            context.can('scopes.delete') && !scope.isSystem ? deleteActions(context, scope) : null),
            context.can('scopes.update') ? panel('Edit', form([
                { name: 'displayName', label: 'Display name', required: true, value: scope.displayName, maxLength: 100 },
                { name: 'description', label: 'Description', type: 'textarea', value: scope.description ?? '', maxLength: 500 }
            ], 'Save', values => run(() => api('PUT', `/scopes/${id}`, {
                displayName: values.displayName,
                description: values.description.trim()
            }), 'The Scope was saved.', context))) : null));
}

function deleteActions(context, scope) {
    const remove = async force => {
        const [label, question] = force
            ? ['Delete and remove', `Delete the Scope ${scope.name} and remove it from all Clients? Their Users lose access to it.`]
            : ['Delete Scope', `Delete the Scope ${scope.name}?`];

        if (await confirmed(label, question)) {
            run(() => api('DELETE', `/scopes/${scope.id}${force ? '?force=true' : ''}`), 'The Scope was deleted.')
                .then(result => result !== undefined && context.navigate('/scopes'));
        }
    };

    return h('div', { class: 'button-row panel-actions' },
        button('Delete Scope', () => remove(false), 'destructive'),
        button('Delete and remove from Clients', () => remove(true), 'destructive'));
}
