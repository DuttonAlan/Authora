// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { api } from '../api.js';
import { badge, button, copyable, details, entityHeader, form, formatDate, h, hint, initials, pageHeader, panel, table } from '../dom.js';
import { auditLink, confirmed, emptyToNull, run } from './common.js';

const presets = [
    { value: 'Generic', label: 'OpenID Connect provider' },
    { value: 'Google', label: 'Google' }
];

/**
 * The list of external identity providers.
 * @param {object} context The context of the view.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function list(context) {
    const providers = await api('GET', '/identity-providers');
    const canManage = context.can('identity-providers.manage');

    return h('div', { class: 'stack' },
        pageHeader('Identity providers', 'Users with a linked account sign in through these providers, in this order on the sign-in page.',
            canManage ? button('New provider', () => context.navigate('/identity-providers?new=1'), 'primary') : null),
        context.parameters.has('new') && canManage ? createPanel(context) : null,
        h('section', { class: 'panel flush' }, table([
            { label: 'Provider', render: provider => provider.name },
            { label: 'Slug', render: provider => h('code', { class: 'token', text: provider.slug }) },
            { label: 'Issuer', class: 'secondary', render: provider => provider.issuer },
            { label: 'Linked accounts', render: provider => String(provider.linkedIdentities) },
            { label: 'Sign-up', render: provider => signUpBadge(provider) },
            { label: 'Status', render: provider => statusBadge(provider) }
        ], providers, { href: provider => `#/identity-providers/${provider.id}`, empty: 'No identity providers exist yet.' })));
}

function statusBadge(provider) {
    return provider.isEnabled ? badge('Enabled', 'ok') : badge('Disabled', 'bad');
}

function signUpBadge(provider) {
    return provider.allowSignUp ? badge('On', 'ok') : badge('Off', 'neutral');
}

function scopesOf(value) {
    return value.split(/\s+/).filter(scope => scope.length > 0);
}

function domainsOf(value) {
    return value.split(/[\s,;]+/).filter(domain => domain.length > 0);
}

function createPanel(context) {
    return panel('New identity provider', form([
        { name: 'preset', label: 'Kind', type: 'select', value: 'Generic', options: presets, help: 'Google always uses the Issuer https://accounts.google.com.' },
        { name: 'name', label: 'Name', required: true, maxLength: 100, help: 'Shown on the button of the sign-in page, e.g. "Google".' },
        { name: 'slug', label: 'Slug', required: true, maxLength: 50, help: 'Lowercase letters, digits and hyphens, e.g. google. Part of the Redirect URI, so it cannot be changed later.' },
        { name: 'issuer', label: 'Issuer', maxLength: 500, help: 'Required unless the kind is Google. Its discovery document is checked when you save.' },
        { name: 'clientId', label: 'Client Id', required: true, maxLength: 500, help: 'The Client Id Authora got from the provider.' },
        { name: 'clientSecret', label: 'Client Secret', type: 'password', maxLength: 500, autocomplete: 'new-password', help: 'Stored encrypted and never shown again. Leave empty if Authora is a Public Client there.' },
        { name: 'scopes', label: 'Scopes', value: 'openid email profile', maxLength: 500, help: 'Space-delimited; openid is always requested.' },
        { name: 'sortOrder', label: 'Order', type: 'number', value: 0, help: 'Lower values come first on the sign-in page.' }
    ], 'Create provider', async values => {
        const created = await run(() => api('POST', '/identity-providers', {
            preset: values.preset,
            name: values.name,
            slug: values.slug.trim(),
            issuer: emptyToNull(values.issuer),
            clientId: values.clientId,
            clientSecret: emptyToNull(values.clientSecret),
            scopes: scopesOf(values.scopes),
            sortOrder: values.sortOrder ?? 0
        }), 'The identity provider was created. Register its Redirect URI at the provider.');

        if (created) {
            context.navigate(`/identity-providers/${created.id}`);
        }
    }, { onCancel: () => context.navigate('/identity-providers') }));
}

/**
 * The page of an external identity provider.
 * @param {object} context The context of the view.
 * @param {string} id The Id of the provider.
 * @returns {Promise<HTMLElement>} The page.
 */
export async function detail(context, id) {
    const canManage = context.can('identity-providers.manage');
    const [provider, groups] = await Promise.all([
        api('GET', `/identity-providers/${id}`),
        canManage && context.can('groups.read') ? api('GET', '/groups') : Promise.resolve(null)
    ]);

    return h('div', { class: 'stack' },
        entityHeader({
            kind: 'Identity provider',
            title: provider.name,
            subtitle: provider.issuer,
            monogram: initials(provider.name),
            status: statusBadge(provider),
            id: provider.id,
            actions: [canManage ? toggleAction(context, provider) : null, auditLink(context, 'identity-provider', provider.id)]
        }),
        h('div', { class: 'grid' },
            panel('Provider', details([
                ['Kind', presets.find(preset => preset.value === provider.preset)?.label ?? provider.preset],
                ['Slug', h('code', { class: 'token', text: provider.slug })],
                ['Redirect URI', copyable(provider.redirectUri, 'Redirect URI')],
                ['Client Id', provider.clientId],
                ['Client Secret', provider.hasClientSecret ? 'Set' : 'None (Public Client)'],
                ['Scopes', h('span', { class: 'badge-row' }, provider.scopes.map(scope => h('code', { class: 'token', text: scope })))],
                ['Order', String(provider.sortOrder)],
                ['Linked accounts', String(provider.linkedIdentities)],
                ['Sign-up', signUpBadge(provider)],
                ['Allowed domains', provider.allowedEmailDomains.length > 0
                    ? h('span', { class: 'badge-row' }, provider.allowedEmailDomains.map(domain => h('code', { class: 'token', text: domain })))
                    : provider.preset === 'Google' ? 'None' : 'Any'],
                ['Default Groups', provider.defaultGroups.length > 0
                    ? h('span', { class: 'badge-row' }, provider.defaultGroups.map(group => badge(group.displayName)))
                    : 'None'],
                ['Created', formatDate(provider.createdAt)],
                ['Modified', formatDate(provider.modifiedAt)]
            ]),
            canManage ? deleteAction(context, provider) : null),
            canManage ? panel('Edit', form([
                { name: 'name', label: 'Name', required: true, value: provider.name, maxLength: 100 },
                {
                    name: 'issuer',
                    label: 'Issuer',
                    required: true,
                    value: provider.issuer,
                    maxLength: 500,
                    disabled: provider.preset === 'Google',
                    help: provider.preset === 'Google' ? 'Fixed for Google.' : 'A new Issuer is checked when you save.'
                },
                { name: 'clientId', label: 'Client Id', required: true, value: provider.clientId, maxLength: 500 },
                { name: 'scopes', label: 'Scopes', value: provider.scopes.join(' '), maxLength: 500 },
                { name: 'sortOrder', label: 'Order', type: 'number', value: provider.sortOrder }
            ], 'Save', values => run(() => api('PUT', `/identity-providers/${id}`, {
                name: values.name,
                issuer: provider.preset === 'Google' ? null : values.issuer,
                clientId: values.clientId,
                scopes: scopesOf(values.scopes),
                sortOrder: values.sortOrder
            }), 'The identity provider was saved.', context))) : null,
            canManage ? signUpPanel(context, provider, groups) : null,
            canManage ? secretPanel(context, provider) : null));
}

function signUpPanel(context, provider, groups) {
    const google = provider.preset === 'Google';
    const fields = [
        {
            name: 'allowSignUp',
            label: 'Create a User on the first sign-in',
            type: 'checkbox',
            value: provider.allowSignUp,
            help: 'Only for an email address the provider verified. An account with the email address of an existing User is never linked automatically: that person must sign in with their existing methods to link it.'
        },
        {
            name: 'allowedEmailDomains',
            label: google ? 'Google Workspace domains' : 'Allowed email domains',
            value: provider.allowedEmailDomains.join(' '),
            help: google
                ? 'Required for sign-up, e.g. example.com. Checked against the Workspace domain of the account, so personal Gmail accounts cannot sign up.'
                : 'Separated by spaces, e.g. example.com. Empty allows every domain: then anyone with an account at the provider can sign up.'
        }
    ];

    // Without the Permission to read Groups, the default Groups are shown above, but cannot be changed here.
    if (groups) {
        fields.push({
            name: 'defaultGroupIds',
            label: 'Default Groups',
            type: 'checks',
            value: provider.defaultGroups.map(group => group.id),
            options: groups.map(group => ({ value: group.id, label: `${group.displayName} (${group.name})` })),
            help: 'New Users join these Groups and get their Roles.'
        });
    }

    return panel('Sign-up',
        hint('Who gets a User on their first sign-in through this provider, and which Groups they join.'),
        form(fields, 'Save sign-up', values => run(() => api('PUT', `/identity-providers/${provider.id}`, {
            allowSignUp: values.allowSignUp,
            allowedEmailDomains: domainsOf(values.allowedEmailDomains),
            defaultGroupIds: groups ? values.defaultGroupIds : null
        }), 'The sign-up settings were saved.', context)));
}

function toggleAction(context, provider) {
    return provider.isEnabled
        ? button('Disable', async () => {
            if (await confirmed('Disable', `Disable ${provider.name}? Users can no longer sign in through it; their linked accounts are kept.`)) {
                run(() => api('POST', `/identity-providers/${provider.id}/disable`), 'The identity provider was disabled.', context);
            }
        })
        : button('Enable', () => run(() => api('POST', `/identity-providers/${provider.id}/enable`), 'The identity provider was enabled.', context));
}

function secretPanel(context, provider) {
    return panel('Client Secret', form([
        { name: 'clientSecret', label: 'New Client Secret', type: 'password', required: true, maxLength: 500, autocomplete: 'new-password', help: 'Replaces the current one. Stored encrypted and never shown again.' }
    ], 'Set secret', (values, element) => run(
        () => api('PUT', `/identity-providers/${provider.id}/secret`, { clientSecret: values.clientSecret }),
        'The Client Secret was saved.',
        context).then(() => element.reset())),
    provider.hasClientSecret
        ? h('div', { class: 'button-row panel-actions' }, button('Remove secret', async () => {
            if (await confirmed('Remove secret', `Remove the Client Secret of ${provider.name}? Authora then authenticates there with PKCE only.`)) {
                run(() => api('DELETE', `/identity-providers/${provider.id}/secret`), 'The Client Secret was removed.', context);
            }
        }, 'destructive'))
        : null);
}

function deleteAction(context, provider) {
    return h('div', { class: 'button-row panel-actions' }, button('Delete provider', async () => {
        if (await confirmed('Delete provider', `Delete the identity provider ${provider.name}? This only works while no accounts are linked through it.`)) {
            run(() => api('DELETE', `/identity-providers/${provider.id}`), 'The identity provider was deleted.')
                .then(result => result !== undefined && context.navigate('/identity-providers'));
        }
    }, 'destructive'));
}
