// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

// Entry point of the admin interface (US-033): signs in, loads the User and their Permissions, and shows the view
// of the current address (hash routing, e.g. #/users/{id}).

import { api } from './api.js';
import { completeSignIn, isSignedIn, loadSettings, signIn, signOut } from './auth.js';
import { h, panel, replace, toast } from './dom.js';
import * as audit from './views/audit.js';
import * as clients from './views/clients.js';
import * as groups from './views/groups.js';
import * as home from './views/home.js';
import * as roles from './views/roles.js';
import * as scopes from './views/scopes.js';
import * as users from './views/users.js';

const id = '([0-9a-fA-F-]{36})';

const routes = [
    { pattern: /^\/$/, view: home.show },
    { pattern: /^\/users$/, view: users.list, permission: 'users.read' },
    { pattern: new RegExp(`^/users/${id}$`), view: users.detail, permission: 'users.read' },
    { pattern: /^\/roles$/, view: roles.list, permission: 'roles.read' },
    { pattern: new RegExp(`^/roles/${id}$`), view: roles.detail, permission: 'roles.read' },
    { pattern: /^\/groups$/, view: groups.list, permission: 'groups.read' },
    { pattern: new RegExp(`^/groups/${id}$`), view: groups.detail, permission: 'groups.read' },
    { pattern: /^\/scopes$/, view: scopes.list, permission: 'scopes.read' },
    { pattern: new RegExp(`^/scopes/${id}$`), view: scopes.detail, permission: 'scopes.read' },
    { pattern: /^\/clients$/, view: clients.list, permission: 'clients.read' },
    { pattern: new RegExp(`^/clients/${id}$`), view: clients.detail, permission: 'clients.read' },
    { pattern: /^\/audit$/, view: audit.list, permission: 'audit.read' }
];

const navigation = [
    { path: '/users', label: 'Users', permission: 'users.read' },
    { path: '/groups', label: 'Groups', permission: 'groups.read' },
    { path: '/roles', label: 'Roles', permission: 'roles.read' },
    { path: '/clients', label: 'Clients', permission: 'clients.read' },
    { path: '/scopes', label: 'Scopes', permission: 'scopes.read' },
    { path: '/audit', label: 'Audit log', permission: 'audit.read' }
];

const main = document.getElementById('main');
let me = null;
let renderCount = 0;

async function start() {
    try {
        await loadSettings();

        if (!await completeSignIn() && !isSignedIn()) {
            await signIn();
            return;
        }

        me = await api('GET', '/me');
    } catch (error) {
        replace(main, panel('The admin interface cannot be opened',
            h('p', { class: 'error', text: error.message }),
            // Starts over without the parameters of a failed authorization response.
            h('button', { type: 'button', class: 'primary', onClick: () => window.location.assign(window.location.pathname) }, 'Sign in again')));
        return;
    }

    document.getElementById('account-name').textContent = me.email;
    document.getElementById('account').hidden = false;
    document.getElementById('sign-out').addEventListener('click', () => signOut());

    window.addEventListener('hashchange', render);
    await render();
}

async function render() {
    const ticket = ++renderCount;
    const { path, parameters } = parseHash();
    const route = routes.find(candidate => candidate.pattern.test(path));

    renderNavigation(path);

    const context = {
        me,
        parameters,
        can: permission => me.permissions.includes(permission),
        navigate: target => { window.location.hash = `#${target}`; },
        reload: render
    };

    if (!route) {
        replace(main, panel('Not found', h('p', { text: 'This page does not exist.' }), h('a', { href: '#/' }, 'Back to the start page')));
        return;
    }

    if (route.permission && !context.can(route.permission)) {
        replace(main, panel('No Permission', h('p', { text: `You need the Permission '${route.permission}' to see this page.` })));
        return;
    }

    try {
        const content = await route.view(context, ...path.match(route.pattern).slice(1));

        // A newer navigation may have started in the meantime.
        if (ticket === renderCount) {
            replace(main, content);
        }
    } catch (error) {
        replace(main, panel('Something went wrong', h('p', { class: 'error', text: error.message })));
        toast(error.message, 'error');
    }
}

function renderNavigation(path) {
    const element = document.getElementById('navigation');
    const items = navigation.filter(item => me.permissions.includes(item.permission));

    replace(element, items.map(item => h('a', {
        href: `#${item.path}`,
        class: path === item.path || path.startsWith(`${item.path}/`) ? 'active' : null
    }, item.label)));

    element.hidden = items.length === 0;
}

function parseHash() {
    const hash = window.location.hash.replace(/^#/, '') || '/';
    const [path, search] = hash.split('?');

    return { path: path || '/', parameters: new URLSearchParams(search ?? '') };
}

start();
