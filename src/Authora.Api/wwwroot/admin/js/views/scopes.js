// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api } from '../api.js';
import { badge, button, details, form, formatDate, h, pageHeader, panel, table } from '../dom.js';
import { auditLink, confirmed, emptyToNull, run } from './common.js';

/**
 * The list of Scopes.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    const scopes = await api('GET', '/scopes');

    return h('div', {},
        pageHeader('Scopes', 'Clients may only request the Scopes an administrator allowed them.',
            context.can('scopes.create') ? button('New Scope', () => context.navigate('/scopes?new=1'), 'primary') : null),
        context.parameters.has('new') && context.can('scopes.create') ? createPanel(context) : null,
        panel(null, table([
            { label: 'Scope', render: scope => h('code', { text: scope.name }) },
            { label: 'Display name', render: scope => scope.displayName },
            { label: 'Description', render: scope => scope.description ?? '' },
            { label: '', render: scope => scope.isSystem ? badge('System') : null }
        ], scopes, { onClick: scope => context.navigate(`/scopes/${scope.id}`), empty: 'No Scopes exist.' })));
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
    }));
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
        pageHeader(scope.displayName, null, auditLink(context, 'scope', scope.id)),
        h('div', { class: 'grid' },
            panel('Scope', details([
                ['Name', h('code', { text: scope.name })],
                ['System Scope', scope.isSystem ? 'Yes, it cannot be deleted' : 'No'],
                ['Created', formatDate(scope.createdAt)],
                ['Modified', formatDate(scope.modifiedAt)],
                ['Id', h('code', { text: scope.id })]
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
    const remove = force => {
        const question = force
            ? `Delete the Scope ${scope.name} and remove it from all Clients? Their Users lose access to it.`
            : `Delete the Scope ${scope.name}?`;

        if (confirmed(question)) {
            run(() => api('DELETE', `/scopes/${scope.id}${force ? '?force=true' : ''}`), 'The Scope was deleted.')
                .then(result => result !== undefined && context.navigate('/scopes'));
        }
    };

    return h('div', { class: 'button-row spaced' },
        button('Delete Scope', () => remove(false), 'danger'),
        button('Delete and remove from Clients', () => remove(true)));
}
