// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api } from '../api.js';
import { badge, button, details, form, formatDate, h, onceOnlyValue, pageHeader, panel, table } from '../dom.js';
import { auditLink, confirmed, run } from './common.js';

const grantTypes = [
    { value: 'authorization_code', label: 'Authorization Code' },
    { value: 'refresh_token', label: 'Refresh Token' }
];

// A Secret is shown only once, right after it was created. It is kept here only until the page is left.
let issuedSecret = null;

/**
 * The list of Clients.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    const clients = await api('GET', '/clients');

    return h('div', {},
        pageHeader('Clients', 'Applications that sign Users in with Authora.',
            context.can('clients.create') ? button('Register Client', () => context.navigate('/clients?new=1'), 'primary') : null),
        context.parameters.has('new') && context.can('clients.create') ? await registerPanel(context) : null,
        panel(null, table([
            { label: 'Client', render: client => client.name },
            { label: 'Type', render: client => client.type },
            { label: 'Status', render: client => statusBadge(client.status) },
            { label: 'Id', render: client => h('code', { text: client.id }) }
        ], clients, { onClick: client => context.navigate(`/clients/${client.id}`), empty: 'No Clients exist.' })));
}

function statusBadge(status) {
    return badge(status, status === 'Enabled' ? 'ok' : 'bad');
}

async function registerPanel(context) {
    const scopes = context.can('scopes.read') ? await api('GET', '/scopes') : [];

    return panel('Register Client', form([
        { name: 'name', label: 'Name', required: true, maxLength: 100 },
        {
            name: 'type', label: 'Type', type: 'select', value: 'Public', options: [
                { value: 'Public', label: 'Public – a browser, mobile or desktop app without a Secret' },
                { value: 'Confidential', label: 'Confidential – a server-side app with a Secret' }
            ]
        },
        { name: 'grantTypes', label: 'Grant Types', type: 'checks', options: grantTypes, value: grantTypes.map(grantType => grantType.value) },
        { name: 'redirectUris', label: 'Redirect URIs', type: 'textarea', help: 'One per line. HTTPS, or HTTP only for 127.0.0.1 and [::1].' },
        { name: 'accessTokenTTL', label: 'Access Token lifetime (seconds)', type: 'number', min: 1, max: 86400, help: 'Defaults to 3600.' },
        { name: 'refreshTokenTTL', label: 'Refresh Token lifetime (seconds)', type: 'number', min: 1, max: 7776000, help: 'Must be longer than the Access Token lifetime.' },
        {
            name: 'scopeIds', label: 'Allowed Scopes', type: 'checks',
            options: scopes.map(scope => ({ value: scope.id, label: scope.name }))
        }
    ], 'Register Client', async values => {
        const registered = await run(() => api('POST', '/clients', {
            name: values.name,
            type: values.type,
            grantTypes: values.grantTypes,
            accessTokenTTL: values.accessTokenTTL,
            refreshTokenTTL: values.refreshTokenTTL,
            redirectUris: lines(values.redirectUris),
            scopeIds: values.scopeIds
        }), 'The Client was registered.');

        if (registered) {
            issuedSecret = registered.secret ? { clientId: registered.id, secret: registered.secret.secret } : null;
            context.navigate(`/clients/${registered.id}`);
        }
    }));
}

function lines(text) {
    return text.split('\n').map(line => line.trim()).filter(line => line !== '');
}

/**
 * The page of a Client.
 * @param {object} context The context of the view.
 * @param {string} id The Id of the Client.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function detail(context, id) {
    const [client, scopes] = await Promise.all([
        api('GET', `/clients/${id}`),
        context.can('scopes.read') && context.can('clients.update') ? api('GET', '/scopes') : Promise.resolve(null)
    ]);

    const secret = issuedSecret?.clientId === id ? issuedSecret.secret : null;
    issuedSecret = null;

    const canUpdate = context.can('clients.update');

    return h('div', { class: 'stack' },
        pageHeader(client.name, `${client.type} Client`, auditLink(context, 'client', client.id)),
        secret ? onceOnlyValue('Client Secret', secret) : null,
        h('div', { class: 'grid' },
            panel('Client',
                details([
                    ['Client Id', h('code', { text: client.id })],
                    ['Status', statusBadge(client.status)],
                    ['Consent', client.isFirstParty ? badge('First-party: not asked') : 'Asked on every sign-in'],
                    ['Grant Types', client.grantTypes.join(', ')],
                    ['Access Token lifetime', `${client.accessTokenTTL} s`],
                    ['Refresh Token lifetime', `${client.refreshTokenTTL} s`],
                    ['Created', formatDate(client.createdAt)],
                    ['Modified', formatDate(client.modifiedAt)]
                ]),
                clientActions(context, client)),
            canUpdate ? panel('Edit', form([
                { name: 'name', label: 'Name', required: true, value: client.name, maxLength: 100 },
                { name: 'grantTypes', label: 'Grant Types', type: 'checks', options: grantTypes, value: client.grantTypes },
                { name: 'accessTokenTTL', label: 'Access Token lifetime (seconds)', type: 'number', min: 1, max: 86400, value: client.accessTokenTTL },
                { name: 'refreshTokenTTL', label: 'Refresh Token lifetime (seconds)', type: 'number', min: 1, max: 7776000, value: client.refreshTokenTTL }
            ], 'Save', values => run(() => api('PUT', `/clients/${id}`, values), 'The Client was saved.', context))) : null),
        redirectUrisPanel(context, client, canUpdate),
        scopesPanel(context, client, scopes, canUpdate),
        client.type === 'Confidential' ? secretsPanel(context, client) : null);
}

function clientActions(context, client) {
    const actions = [];

    if (context.can('clients.update')) {
        actions.push(client.status === 'Enabled'
            ? button('Disable', () => {
                if (confirmed(`Disable ${client.name}? All its Grants and tokens are revoked.`)) {
                    run(() => api('POST', `/clients/${client.id}/disable`), 'The Client was disabled.', context);
                }
            })
            : button('Enable', () => run(() => api('POST', `/clients/${client.id}/enable`), 'The Client was enabled.', context)));
    }

    if (context.can('clients.manage-first-party')) {
        actions.push(client.isFirstParty
            ? button('Ask for consent', () => run(() => api('DELETE', `/clients/${client.id}/first-party`), 'Users are asked for consent again.', context))
            : button('Mark as first-party', () => {
                if (confirmed(`Mark ${client.name} as first-party? Users are no longer asked for consent, so it gets every Scope it is allowed without asking. Only for Clients of your own deployment.`)) {
                    run(() => api('PUT', `/clients/${client.id}/first-party`), 'The Client is first-party.', context);
                }
            }));
    }

    if (context.can('clients.delete')) {
        actions.push(button('Delete', () => {
            if (confirmed(`Delete ${client.name}? All its Grants and tokens are deleted. This cannot be undone.`)) {
                run(() => api('DELETE', `/clients/${client.id}`), 'The Client was deleted.')
                    .then(result => result !== undefined && context.navigate('/clients'));
            }
        }, 'danger'));
    }

    return actions.length > 0 ? h('div', { class: 'button-row spaced' }, actions) : null;
}

function redirectUrisPanel(context, client, canUpdate) {
    const input = h('input', { type: 'url', placeholder: 'https://app.example.com/callback', 'aria-label': 'Redirect URI' });

    return panel('Redirect URIs',
        table([
            { label: 'Redirect URI', render: uri => h('code', { text: uri }) },
            {
                label: '', class: 'actions-cell', render: uri => canUpdate
                    ? button('Remove', () => run(
                        () => api('DELETE', `/clients/${client.id}/redirect-uris?uri=${encodeURIComponent(uri)}`),
                        'The Redirect URI was removed.',
                        context), 'small')
                    : null
            }
        ], client.redirectUris, { empty: 'The Client has no Redirect URIs, so it cannot sign Users in.' }),
        canUpdate
            ? h('form', {
                class: 'inline spaced', onSubmit: event => {
                    event.preventDefault();
                    run(() => api('POST', `/clients/${client.id}/redirect-uris`, { uri: input.value.trim() }), 'The Redirect URI was added.', context);
                }
            }, input, h('button', { type: 'submit', class: 'primary' }, 'Add'))
            : null);
}

function scopesPanel(context, client, scopes, canUpdate) {
    const allowed = new Set(client.scopes.map(scope => scope.id));
    const served = new Set(client.servedScopes.map(scope => scope.id));

    if (!canUpdate || scopes === null) {
        return panel('Scopes', details([
            ['Allowed', client.scopes.map(scope => scope.name).join(', ') || '–'],
            ['Served', client.servedScopes.map(scope => scope.name).join(', ') || '–']
        ]));
    }

    const toggle = (path, scope, enabled, label) => run(
        () => api(enabled ? 'PUT' : 'DELETE', `/clients/${client.id}/${path}/${scope.id}`),
        `${scope.name}: ${label} ${enabled ? 'added' : 'removed'}.`,
        context);

    return panel('Scopes',
        h('p', { class: 'muted', text: 'Allowed: the Client may request the Scope. Served: the Client is a resource server for the Scope and may introspect tokens with it (Confidential Clients only).' }),
        table([
            { label: 'Scope', render: scope => h('code', { text: scope.name }) },
            {
                label: 'Allowed', render: scope => h('input', {
                    type: 'checkbox',
                    'aria-label': `Allow ${scope.name}`,
                    checked: allowed.has(scope.id),
                    onChange: event => toggle('scopes', scope, event.target.checked, 'allowed')
                })
            },
            {
                label: 'Served', render: scope => h('input', {
                    type: 'checkbox',
                    'aria-label': `Serve ${scope.name}`,
                    checked: served.has(scope.id),
                    disabled: client.type !== 'Confidential' || scope.isSystem,
                    onChange: event => toggle('served-scopes', scope, event.target.checked, 'serving')
                })
            }
        ], scopes, { empty: 'No Scopes exist.' }));
}

function secretsPanel(context, client) {
    const canManage = context.can('clients.manage-secrets');

    return panel('Secrets',
        table([
            { label: 'Id', render: secret => h('code', { text: secret.id }) },
            { label: 'Created', render: secret => formatDate(secret.createdAt) },
            { label: 'Expires', render: secret => formatDate(secret.expiresAt) },
            { label: 'Status', render: secret => secret.isActive ? badge('Active', 'ok') : badge(secret.revokedAt ? 'Revoked' : 'Expired', 'bad') },
            {
                label: '', class: 'actions-cell', render: secret => canManage && !secret.revokedAt
                    ? button('Revoke', () => {
                        if (confirmed('Revoke this Secret? The Client can no longer authenticate with it.')) {
                            run(() => api('POST', `/clients/${client.id}/secrets/${secret.id}/revoke`), 'The Secret was revoked.', context);
                        }
                    }, 'small')
                    : null
            }
        ], client.secrets, { empty: 'The Client has no Secrets.' }),
        canManage ? h('div', { class: 'spaced' }, form([
            { name: 'expiresAt', label: 'Expires at', type: 'datetime-local', help: 'Optional, at most 730 days from now. Defaults to 365 days.' }
        ], 'Create Secret', async values => {
            const secret = await run(() => api('POST', `/clients/${client.id}/secrets`, {
                expiresAt: values.expiresAt ? new Date(values.expiresAt).toISOString() : null
            }), 'The Secret was created.');

            if (secret) {
                // The page is shown again with the new Secret at the top.
                issuedSecret = { clientId: client.id, secret: secret.secret };
                await context.reload();
            }
        })) : null);
}
