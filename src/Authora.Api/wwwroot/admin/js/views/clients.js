// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api } from '../api.js';
import { badge, button, copyable, details, entityHeader, form, formatDate, h, hint, initials, onceOnlyValue, pageHeader, panel, table } from '../dom.js';
import { auditLink, confirmed, run } from './common.js';

const grantTypes = [
    { value: 'authorization_code', label: 'Authorization Code' },
    { value: 'refresh_token', label: 'Refresh Token' },
    { value: 'client_credentials', label: 'Client Credentials – tokens for the Client itself, without a User (Confidential only)' }
];

// Client Credentials is opt-in, and only Confidential Clients can use it. The server refuses to newly allow it for a
// first-party Client, so it is only offered there when already allowed, to keep or remove it.
const defaultGrantTypes = ['authorization_code', 'refresh_token'];
const canHaveClientCredentials = client => client.type === 'Confidential'
    && (!client.isFirstParty || client.grantTypes.includes('client_credentials'));
const grantTypesFor = client => canHaveClientCredentials(client)
    ? grantTypes
    : grantTypes.filter(grantType => grantType.value !== 'client_credentials');

// A Secret is shown only once, right after it was created. It is kept here only until the page is left.
let issuedSecret = null;

/**
 * The list of Clients.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    const clients = await api('GET', '/clients');

    return h('div', { class: 'stack' },
        pageHeader('Clients', 'Applications that sign Users in with Authora.',
            context.can('clients.create') ? button('Register Client', () => context.navigate('/clients?new=1'), 'primary') : null),
        context.parameters.has('new') && context.can('clients.create') ? await registerPanel(context) : null,
        h('section', { class: 'panel flush' }, table([
            {
                label: 'Client', render: client => h('span', { class: 'person' },
                    h('span', { class: 'avatar square', 'aria-hidden': 'true', text: initials(client.name).toUpperCase() }),
                    h('span', { class: 'person-text' },
                        h('span', { class: 'person-name', text: client.name }),
                        h('code', { class: 'person-email', text: client.id })))
            },
            { label: 'Type', render: client => client.type },
            { label: 'Status', render: client => statusBadge(client.status) }
        ], clients, { href: client => `#/clients/${client.id}`, empty: 'No Clients exist yet. Register one to let an application sign Users in.' })));
}

/**
 * A badge for the Status of a Client.
 * @param {string} status The Status.
 * @returns {HTMLElement} The badge.
 */
export function statusBadge(status) {
    return badge(status, status === 'Enabled' ? 'ok' : 'bad');
}

async function registerPanel(context) {
    const scopes = context.can('scopes.read') ? await api('GET', '/scopes') : [];

    const element = form([
        { name: 'name', label: 'Name', required: true, maxLength: 100 },
        {
            name: 'type', label: 'Type', type: 'select', value: 'Public', options: [
                { value: 'Public', label: 'Public – a browser, mobile or desktop app without a Secret' },
                { value: 'Confidential', label: 'Confidential – a server-side app with a Secret' }
            ]
        },
        { name: 'grantTypes', label: 'Grant Types', type: 'checks', options: grantTypes, value: defaultGrantTypes },
        { name: 'redirectUris', label: 'Redirect URIs', type: 'textarea', help: 'One per line. HTTPS, or HTTP only for 127.0.0.1 and [::1].' },
        { name: 'postLogoutRedirectUris', label: 'Post-logout Redirect URIs', type: 'textarea', help: 'Optional. Where the application may send Users back after signing them out. One per line, same rules.' },
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
            postLogoutRedirectUris: lines(values.postLogoutRedirectUris),
            scopeIds: values.scopeIds
        }), 'The Client was registered.');

        if (registered) {
            issuedSecret = registered.secret ? { clientId: registered.id, secret: registered.secret.secret } : null;
            context.navigate(`/clients/${registered.id}`);
        }
    }, { onCancel: () => context.navigate('/clients') });

    // Client Credentials is only for Confidential Clients: hide and clear it while Public is selected.
    const type = element.querySelector('select[name="type"]');
    const clientCredentials = element.querySelector('input[name="grantTypes"][value="client_credentials"]');
    const showGrantTypes = () => {
        const confidential = type.value === 'Confidential';
        clientCredentials.closest('label').hidden = !confidential;

        if (!confidential) {
            clientCredentials.checked = false;
        }
    };

    type.addEventListener('change', showGrantTypes);
    showGrantTypes();

    return panel('Register Client', element);
}

function lines(text) {
    return text.split('\n').map(line => line.trim()).filter(line => line !== '');
}

function duration(seconds) {
    const units = [['day', 86400], ['hour', 3600], ['minute', 60]];
    const [unit, size] = units.find(([, length]) => seconds >= length && seconds % length === 0) ?? [null, 1];

    return unit ? `${seconds / size} ${unit}${seconds / size === 1 ? '' : 's'} (${seconds.toLocaleString()} s)` : `${seconds.toLocaleString()} s`;
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
        entityHeader({
            kind: `${client.type} Client`,
            title: client.name,
            monogram: initials(client.name),
            status: h('span', { class: 'badge-row' }, statusBadge(client.status), client.isFirstParty ? badge('First-party') : null),
            actions: [auditLink(context, 'client', client.id)]
        }),
        secret ? onceOnlyValue('Client Secret', secret) : null,
        h('div', { class: 'grid' },
            panel('Client',
                details([
                    ['Client Id', copyable(client.id, 'Client Id')],
                    ['Consent', client.isFirstParty ? 'First-party: not asked' : 'Asked on every sign-in'],
                    ['PKCE', client.isPkceOptional ? badge('Optional', 'warn') : 'Required'],
                    ['Multi-factor authentication', client.requiresMfa ? 'Required' : 'Optional'],
                    ['Grant Types', h('span', { class: 'badge-row' }, client.grantTypes.map(grantType => h('code', { class: 'token', text: grantType })))],
                    ['Access Token lifetime', duration(client.accessTokenTTL)],
                    ['Refresh Token lifetime', duration(client.refreshTokenTTL)],
                    ['Back-channel logout', client.backchannelLogoutUri
                        ? h('span', { class: 'badge-row' },
                            h('code', { text: client.backchannelLogoutUri }),
                            client.backchannelLogoutSessionRequired ? badge('sid required') : null)
                        : 'Not notified'],
                    ['Created', formatDate(client.createdAt)],
                    ['Modified', formatDate(client.modifiedAt)]
                ]),
                clientActions(context, client)),
            canUpdate ? panel('Edit', form([
                { name: 'name', label: 'Name', required: true, value: client.name, maxLength: 100 },
                { name: 'grantTypes', label: 'Grant Types', type: 'checks', options: grantTypesFor(client), value: client.grantTypes },
                { name: 'accessTokenTTL', label: 'Access Token lifetime (seconds)', type: 'number', min: 1, max: 86400, value: client.accessTokenTTL },
                { name: 'refreshTokenTTL', label: 'Refresh Token lifetime (seconds)', type: 'number', min: 1, max: 7776000, value: client.refreshTokenTTL },
                {
                    name: 'backchannelLogoutUri',
                    label: 'Back-Channel Logout URI',
                    type: 'url',
                    maxLength: 2048,
                    placeholder: 'https://app.example.com/backchannel-logout',
                    value: client.backchannelLogoutUri,
                    help: 'Receives a Logout Token when a Session ends in which the Client got tokens. Leave empty to not notify the Client.'
                },
                {
                    name: 'backchannelLogoutSessionRequired',
                    label: 'The Client requires sid in Logout Tokens',
                    type: 'checkbox',
                    value: client.backchannelLogoutSessionRequired,
                    help: 'Authora always sends sid; this records what the Client expects.'
                }
            ], 'Save', values => run(() => api('PUT', `/clients/${id}`, values), 'The Client was saved.', context))) : null),
        redirectUrisPanel(context, client, canUpdate),
        postLogoutRedirectUrisPanel(context, client, canUpdate),
        scopesPanel(context, client, scopes, canUpdate),
        client.type === 'Confidential' ? apiPanel(context, client, canUpdate) : null,
        client.type === 'Confidential' ? secretsPanel(context, client) : null);
}

// A Confidential Client with a Resource Identifier is an API (RFC 8707): other Clients name it with the resource
// parameter, and it sees only tokens meant for it at introspection.
function apiPanel(context, client, canUpdate) {
    const summary = details([
        ['Resource Identifier', client.resourceIdentifier ? copyable(client.resourceIdentifier, 'Resource Identifier') : 'None: not an API'],
        ['resource parameter', client.resourceIdentifier
            ? (client.requiresResourceIndicator ? 'Required: only tokens that name the API' : 'Optional: also tokens for its served Scopes')
            : '–']
    ]);

    if (!canUpdate) {
        return panel('API', summary);
    }

    return panel('API',
        hint('Clients request tokens for an API with resource=<Resource Identifier>. Such a token keeps only the Scopes the API serves, and only the API (and the Client it was issued to) can introspect it.'),
        summary,
        h('div', { class: 'subsection' }, form([
            {
                name: 'resourceIdentifier',
                label: 'Resource Identifier',
                type: 'url',
                maxLength: 2048,
                placeholder: 'https://api.example.com',
                value: client.resourceIdentifier,
                help: 'Absolute HTTPS URI without query or fragment, unique among all Clients. Leave empty to make the Client no API.'
            },
            {
                name: 'requiresResourceIndicator',
                label: 'Require the resource parameter',
                type: 'checkbox',
                value: client.requiresResourceIndicator,
                help: 'Tokens requested without resource are then no longer meant for this API, even if it serves one of their Scopes.'
            }
        ], 'Save', values => run(() => api('PUT', `/clients/${client.id}`, {
            resourceIdentifier: values.resourceIdentifier.trim(),
            // Without a Resource Identifier the setting is removed with it, so it is not sent.
            requiresResourceIndicator: values.resourceIdentifier.trim() ? values.requiresResourceIndicator : null
        }), 'The API settings were saved.', context))));
}

function clientActions(context, client) {
    const actions = [];

    if (context.can('clients.update')) {
        actions.push(client.status === 'Enabled'
            ? button('Disable', async () => {
                if (await confirmed('Disable', `Disable ${client.name}? All its Grants and tokens are revoked.`)) {
                    run(() => api('POST', `/clients/${client.id}/disable`), 'The Client was disabled.', context);
                }
            })
            : button('Enable', () => run(() => api('POST', `/clients/${client.id}/enable`), 'The Client was enabled.', context)));

        // Public Clients always need PKCE, and the server refuses to make it optional for them and for first-party
        // Clients. Requiring it again is always possible.
        if (client.isPkceOptional) {
            actions.push(button('Require PKCE', () => run(() => api('DELETE', `/clients/${client.id}/pkce-optional`), 'PKCE is required again.', context)));
        } else if (client.type === 'Confidential' && !client.isFirstParty) {
            actions.push(button('Make PKCE optional', async () => {
                if (await confirmed('Make PKCE optional', `Make PKCE optional for ${client.name}? Only for OpenID Connect Clients that cannot send PKCE: they must send a nonce in every authorization request instead.`)) {
                    run(() => api('PUT', `/clients/${client.id}/pkce-optional`), 'PKCE is optional for the Client.', context);
                }
            }));
        }
    }

    if (context.can('clients.update')) {
        actions.push(client.requiresMfa
            ? button('Allow without MFA', async () => {
                if (await confirmed('Allow without MFA', `Allow ${client.name} without multi-factor authentication? Users can then use it with their password alone.`)) {
                    run(() => api('DELETE', `/clients/${client.id}/mfa-required`), 'Multi-factor authentication is no longer required.', context);
                }
            })
            : button('Require MFA', async () => {
                if (await confirmed('Require MFA', `Require multi-factor authentication for ${client.name}? Users without a second factor must set up an authenticator app before they can use it.`)) {
                    run(() => api('PUT', `/clients/${client.id}/mfa-required`), 'Multi-factor authentication is required.', context);
                }
            }));
    }

    if (context.can('clients.manage-first-party')) {
        actions.push(client.isFirstParty
            ? button('Ask for consent', () => run(() => api('DELETE', `/clients/${client.id}/first-party`), 'Users are asked for consent again.', context))
            : button('Mark as first-party', async () => {
                if (await confirmed('Mark as first-party', `Mark ${client.name} as first-party? Users are no longer asked for consent, so it gets every Scope it is allowed without asking. Only for Clients of your own deployment.`)) {
                    run(() => api('PUT', `/clients/${client.id}/first-party`), 'The Client is first-party.', context);
                }
            }));
    }

    if (context.can('clients.delete')) {
        actions.push(button('Delete', async () => {
            if (await confirmed('Delete', `Delete ${client.name}? All its Grants and tokens are deleted. This cannot be undone.`)) {
                run(() => api('DELETE', `/clients/${client.id}`), 'The Client was deleted.')
                    .then(result => result !== undefined && context.navigate('/clients'));
            }
        }, 'destructive'));
    }

    return actions.length > 0 ? h('div', { class: 'button-row panel-actions' }, actions) : null;
}

// A first-party Client gets codes without asking the User, so the server refuses new Redirect URIs, Post-logout
// Redirect URIs and Scopes for it.
const firstPartyLockedText = 'This Client is first-party, so no Redirect URIs, Post-logout Redirect URIs or Scopes can be added. Mark it as third-party first to change them.';

function uriList(uris, empty, remove) {
    if (uris.length === 0) {
        return h('p', { class: 'empty', text: empty });
    }

    return h('ul', { class: 'uri-list' }, uris.map(uri => h('li', {},
        h('code', { text: uri }),
        remove ? button('Remove', () => remove(uri), 'small') : null)));
}

function redirectUrisPanel(context, client, canUpdate) {
    const input = h('input', { type: 'url', placeholder: 'https://app.example.com/callback', 'aria-label': 'Redirect URI' });

    return panel('Redirect URIs',
        hint('Where Authora may send the authorization code. Matched exactly.'),
        uriList(client.redirectUris, 'The Client has no Redirect URIs, so it cannot sign Users in.', canUpdate
            ? uri => run(() => api('DELETE', `/clients/${client.id}/redirect-uris?uri=${encodeURIComponent(uri)}`), 'The Redirect URI was removed.', context)
            : null),
        canUpdate && client.isFirstParty
            ? h('p', { class: 'hint locked', text: firstPartyLockedText })
            : null,
        canUpdate && !client.isFirstParty
            ? h('form', {
                class: 'inline add-row', onSubmit: event => {
                    event.preventDefault();
                    run(() => api('POST', `/clients/${client.id}/redirect-uris`, { uri: input.value.trim() }), 'The Redirect URI was added.', context);
                }
            }, input, h('button', { type: 'submit', class: 'primary' }, 'Add'))
            : null);
}

// Post-logout Redirect URIs receive no code, but the Client Id of a first-party Client like the admin interface is
// public, so a new one would make its logout an open redirect. The server refuses them like Redirect URIs.
function postLogoutRedirectUrisPanel(context, client, canUpdate) {
    const input = h('input', { type: 'url', placeholder: 'https://app.example.com/signed-out', 'aria-label': 'Post-logout Redirect URI' });

    return panel('Post-logout Redirect URIs',
        hint('Where the application may send Users back after signing them out.'),
        uriList(client.postLogoutRedirectUris, 'None: after signing a User out, Authora shows its own page instead of returning to the application.', canUpdate
            ? uri => run(() => api('DELETE', `/clients/${client.id}/post-logout-redirect-uris?uri=${encodeURIComponent(uri)}`), 'The Post-logout Redirect URI was removed.', context)
            : null),
        canUpdate && client.isFirstParty
            ? h('p', { class: 'hint locked', text: firstPartyLockedText })
            : null,
        canUpdate && !client.isFirstParty
            ? h('form', {
                class: 'inline add-row', onSubmit: event => {
                    event.preventDefault();
                    run(() => api('POST', `/clients/${client.id}/post-logout-redirect-uris`, { uri: input.value.trim() }), 'The Post-logout Redirect URI was added.', context);
                }
            }, input, h('button', { type: 'submit', class: 'primary' }, 'Add'))
            : null);
}

function scopesPanel(context, client, scopes, canUpdate) {
    const allowed = new Set(client.scopes.map(scope => scope.id));
    const served = new Set(client.servedScopes.map(scope => scope.id));
    const tokens = list => list.length > 0
        ? h('span', { class: 'badge-row' }, list.map(scope => h('code', { class: 'token', text: scope.name })))
        : '–';

    if (!canUpdate || scopes === null) {
        return panel('Scopes', details([
            ['Allowed', tokens(client.scopes)],
            ['Served', tokens(client.servedScopes)]
        ]));
    }

    const toggle = (path, scope, enabled, label) => run(
        () => api(enabled ? 'PUT' : 'DELETE', `/clients/${client.id}/${path}/${scope.id}`),
        `${scope.name}: ${label} ${enabled ? 'added' : 'removed'}.`,
        context);

    return panel('Scopes',
        hint('Allowed: the Client may request the Scope. Served: the Client is a resource server for the Scope and may introspect tokens with it that are meant for it (Confidential Clients only).'),
        client.isFirstParty ? h('p', { class: 'hint locked', text: firstPartyLockedText }) : null,
        table([
            { label: 'Scope', render: scope => h('code', { class: 'token', text: scope.name }) },
            {
                label: 'Allowed', class: 'check-cell', render: scope => h('input', {
                    type: 'checkbox',
                    'aria-label': `Allow ${scope.name}`,
                    checked: allowed.has(scope.id),
                    // Allowed Scopes of a first-party Client can be removed, but none added (the server rejects it).
                    disabled: client.isFirstParty && !allowed.has(scope.id),
                    onChange: event => toggle('scopes', scope, event.target.checked, 'allowed')
                })
            },
            {
                label: 'Served', class: 'check-cell', render: scope => h('input', {
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
        hint('The Client authenticates at the token endpoint with one of its active Secrets. Authora stores only their hashes.'),
        table([
            { label: 'Id', render: secret => h('code', { text: secret.id }) },
            { label: 'Created', render: secret => formatDate(secret.createdAt) },
            { label: 'Expires', render: secret => formatDate(secret.expiresAt) },
            { label: 'Status', render: secret => secret.isActive ? badge('Active', 'ok') : badge(secret.revokedAt ? 'Revoked' : 'Expired', 'bad') },
            {
                label: '', class: 'actions-cell', render: secret => canManage && !secret.revokedAt
                    ? button('Revoke', async () => {
                        if (await confirmed('Revoke', 'Revoke this Secret? The Client can no longer authenticate with it.')) {
                            run(() => api('POST', `/clients/${client.id}/secrets/${secret.id}/revoke`), 'The Secret was revoked.', context);
                        }
                    }, 'small')
                    : null
            }
        ], client.secrets, { empty: 'The Client has no Secrets.' }),
        canManage ? h('div', { class: 'subsection' }, form([
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
