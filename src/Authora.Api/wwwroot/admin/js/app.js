// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

// Entry point of the admin interface: signs in, loads the User and their Permissions, and shows the view
// of the current address (hash routing, e.g. #/users/{id}).

import { api } from './api.js';
import { basePath, signIn, signOut, takeSignInError } from './auth.js';
import { h, initials, replace, toast } from './dom.js';
import { icon } from './icons.js';
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
    // Users who may only verify the chain see the page without the events.
    { pattern: /^\/audit$/, view: audit.list, permission: ['audit.read', 'audit.verify'] }
];

// Two levels at most: a group heading and its pages.
const navigation = [
    { items: [{ path: '/', label: 'Overview', icon: 'overview' }] },
    {
        label: 'Directory', items: [
            { path: '/users', label: 'Users', icon: 'users', permission: 'users.read' },
            { path: '/groups', label: 'Groups', icon: 'groups', permission: 'groups.read' },
            { path: '/roles', label: 'Roles', icon: 'roles', permission: 'roles.read' }
        ]
    },
    {
        label: 'Applications', items: [
            { path: '/clients', label: 'Clients', icon: 'clients', permission: 'clients.read' },
            { path: '/scopes', label: 'Scopes', icon: 'scopes', permission: 'scopes.read' }
        ]
    },
    {
        label: 'Security', items: [
            { path: '/audit', label: 'Audit log', icon: 'audit', permission: ['audit.read', 'audit.verify'] }
        ]
    }
];

const main = document.getElementById('main');
const app = document.getElementById('app');
const sidebar = document.getElementById('sidebar');
const scrim = document.getElementById('scrim');
const menuButton = document.getElementById('menu-button');
const content = document.querySelector('.content');
const narrow = window.matchMedia('(max-width: 899px)');
let me = null;
let renderCount = 0;

async function start() {
    menuButton.appendChild(icon('menu'));
    setupDrawer();

    // The address holds the route, so the skip link moves the focus instead of changing the hash.
    document.querySelector('.skip-link').addEventListener('click', event => {
        event.preventDefault();
        main.focus();
    });

    try {
        const signInError = takeSignInError();

        if (signInError !== null) {
            throw new Error(signInError);
        }

        // Without a valid cookie, this signs in and comes back here.
        me = await api('GET', '/me');
    } catch (error) {
        app.classList.add('signed-out');
        replace(main, h('div', { class: 'blocked' },
            h('img', { src: 'logo.svg', alt: '', width: 48, height: 48 }),
            h('h1', { text: 'The admin interface cannot be opened' }),
            h('p', { class: 'error', text: error.message }),
            h('button', { type: 'button', class: 'primary', onClick: () => signIn() }, 'Sign in again')));
        return;
    }

    document.getElementById('account-avatar').textContent = initials(me.firstName, me.lastName).toUpperCase();
    document.getElementById('account-name').textContent = `${me.firstName} ${me.lastName}`;
    document.getElementById('account-email').textContent = me.email;
    document.getElementById('account').hidden = false;

    // The page of Authora itself, which offers to come back here afterwards.
    const changePassword = document.getElementById('change-password');
    changePassword.href = `${basePath()}/account/password?returnUrl=${encodeURIComponent(`${basePath()}/admin/`)}`;
    changePassword.appendChild(icon('password'));

    const signOutButton = document.getElementById('sign-out');
    signOutButton.appendChild(icon('signOut'));
    signOutButton.addEventListener('click', () => signOut());

    window.addEventListener('hashchange', async () => {
        closeDrawer(false);
        const ticket = await render();

        // A newer navigation has started meanwhile; the focus and the scroll position are its business.
        if (ticket !== renderCount) {
            return;
        }

        // Screen readers and keyboard users continue at the new content, not at the link they chose.
        main.focus({ preventScroll: true });
        window.scrollTo(0, 0);
    });

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
        show(ticket, path, message('Not found', 'This page does not exist.', h('a', { class: 'button', href: '#/' }, 'Back to the overview')));
        return ticket;
    }

    if (!isAllowed(route.permission)) {
        const names = [route.permission].flat().map(name => `'${name}'`).join(' or ');
        show(ticket, path, message('No Permission', `You need the Permission ${names} to see this page.`));
        return ticket;
    }

    main.setAttribute('aria-busy', 'true');

    try {
        const content = await route.view(context, ...path.match(route.pattern).slice(1));
        show(ticket, path, content);
    } catch (error) {
        show(ticket, path, message('Something went wrong', error.message));
        toast(error.message, 'error');
    } finally {
        main.removeAttribute('aria-busy');
    }

    return ticket;
}

// A route or navigation item needs one Permission, any one of a list, or none.
function isAllowed(permission) {
    return !permission || [permission].flat().some(name => me.permissions.includes(name));
}

// A newer navigation may have started in the meantime; then its content wins.
function show(ticket, path, content) {
    if (ticket !== renderCount) {
        return;
    }

    replace(main, content);

    const title = main.querySelector('h1')?.textContent ?? 'Authora';
    document.title = `${title} – Authora Admin`;
    renderBreadcrumb(path, title);
}

function message(title, text, ...actions) {
    return h('div', { class: 'stack' },
        h('header', { class: 'page-header' }, h('div', { class: 'page-heading' }, h('h1', { text: title }))),
        h('section', { class: 'panel' }, h('p', { text }), actions.length > 0 ? h('div', { class: 'button-row' }, actions) : null));
}

function visibleItems(group) {
    return group.items.filter(item => isAllowed(item.permission));
}

function sectionOf(path) {
    return navigation.flatMap(group => group.items)
        .find(item => item.path !== '/' && (path === item.path || path.startsWith(`${item.path}/`)));
}

function renderNavigation(path) {
    const element = document.getElementById('navigation');
    const current = sectionOf(path)?.path ?? (path === '/' ? '/' : null);

    replace(element, navigation.map(group => {
        const items = visibleItems(group);

        if (items.length === 0) {
            return null;
        }

        return h('div', { class: 'nav-group' },
            group.label ? h('p', { class: 'nav-heading', text: group.label }) : null,
            h('ul', {}, items.map(item => h('li', {},
                h('a', { href: `#${item.path}`, class: item.path === current ? 'active' : null, 'aria-current': item.path === current ? 'page' : null },
                    icon(item.icon), h('span', { text: item.label }))))));
    }));
}

function renderBreadcrumb(path, title) {
    const element = document.getElementById('breadcrumb');
    const section = sectionOf(path);
    const isDetail = section && path !== section.path;

    replace(element, h('ol', {},
        isDetail
            ? [h('li', {}, h('a', { href: `#${section.path}` }, section.label)), h('li', {}, h('span', { 'aria-current': 'page', text: title }))]
            : h('li', {}, h('span', { 'aria-current': 'page', text: section?.label ?? (path === '/' ? 'Overview' : title) }))));
}

// On narrow screens, the sidebar is a drawer over the content. While closed, it is inert, so keyboard focus
// cannot reach links that are off screen. While open, the content behind the scrim is inert instead.
function setupDrawer() {
    const apply = () => {
        if (narrow.matches) {
            sidebar.inert = !app.classList.contains('nav-open');
        } else {
            app.classList.remove('nav-open');
            sidebar.inert = false;
            content.inert = false;
            scrim.hidden = true;
            menuButton.setAttribute('aria-expanded', 'false');
        }
    };

    menuButton.addEventListener('click', () => (app.classList.contains('nav-open') ? closeDrawer(true) : openDrawer()));
    scrim.addEventListener('click', () => closeDrawer(true));
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && app.classList.contains('nav-open')) {
            closeDrawer(true);
        }
    });
    narrow.addEventListener('change', apply);
    apply();
}

function openDrawer() {
    app.classList.add('nav-open');
    sidebar.inert = false;
    content.inert = true;
    scrim.hidden = false;
    menuButton.setAttribute('aria-expanded', 'true');
    menuButton.setAttribute('aria-label', 'Hide navigation');
    sidebar.querySelector('a')?.focus();
}

function closeDrawer(returnFocus) {
    if (!app.classList.contains('nav-open')) {
        return;
    }

    app.classList.remove('nav-open');
    sidebar.inert = narrow.matches;
    content.inert = false;
    scrim.hidden = true;
    menuButton.setAttribute('aria-expanded', 'false');
    menuButton.setAttribute('aria-label', 'Show navigation');

    if (returnFocus) {
        menuButton.focus();
    }
}

function parseHash() {
    const hash = window.location.hash.replace(/^#/, '') || '/';
    const [path, search] = hash.split('?');

    return { path: path || '/', parameters: new URLSearchParams(search ?? '') };
}

start();
