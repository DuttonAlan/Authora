// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

// Signs in with Authora's own Authorization Code flow with PKCE, like any other browser-based Client.
// The tokens are kept in sessionStorage, so they are gone when the tab is closed and are not shared with other tabs.
// The Content Security Policy of the admin interface allows no inline or foreign scripts, which could read them.

const tokenKey = 'authora.admin.tokens';
const pendingKey = 'authora.admin.pending';

// Refresh a little before the Access Token expires, so a request does not fail on the way.
const refreshMarginSeconds = 30;

let settings = null;
let refreshing = null;

/**
 * The path Authora is served at, e.g. "" or "/auth", taken from the URL of the admin interface.
 * @returns {string} The base path.
 */
export function basePath() {
    const path = window.location.pathname;
    return path.slice(0, path.lastIndexOf('/admin/'));
}

/**
 * Loads the Client Id and the Issuer of the admin interface.
 * @returns {Promise<object>} The settings.
 */
export async function loadSettings() {
    const response = await fetch(`${basePath()}/admin/settings`, { headers: { Accept: 'application/json' } });

    if (!response.ok) {
        const problem = await response.json().catch(() => null);
        throw new Error(problem?.detail ?? 'The admin interface is not available.');
    }

    settings = await response.json();
    return settings;
}

/**
 * Whether tokens are stored.
 * @returns {boolean} True if signed in.
 */
export function isSignedIn() {
    return readTokens() !== null;
}

/**
 * Sends the browser to the authorization endpoint. Returns to the current view afterwards.
 * @returns {Promise<void>} Never completes, because the page is left.
 */
export async function signIn() {
    const verifier = randomString(64);
    const state = randomString(32);
    const challenge = base64Url(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier)));

    sessionStorage.setItem(pendingKey, JSON.stringify({ state, verifier, returnTo: window.location.hash }));

    const parameters = new URLSearchParams({
        response_type: 'code',
        client_id: settings.clientId,
        redirect_uri: settings.redirectUri,
        scope: settings.scope,
        state,
        code_challenge: challenge,
        code_challenge_method: 'S256'
    });

    window.location.assign(`${basePath()}/oauth/authorize?${parameters}`);

    return new Promise(() => { });
}

/**
 * Completes the sign-in if the page was opened with an authorization response.
 * @returns {Promise<boolean>} True if an authorization response was handled.
 * @throws {Error} If the response is an error or does not belong to this browser.
 */
export async function completeSignIn() {
    const parameters = new URLSearchParams(window.location.search);

    if (!parameters.has('code') && !parameters.has('error')) {
        return false;
    }

    const pending = JSON.parse(sessionStorage.getItem(pendingKey) ?? 'null');
    sessionStorage.removeItem(pendingKey);

    // Removes the code from the address bar and the history.
    window.history.replaceState(null, '', `${window.location.pathname}${pending?.returnTo ?? ''}`);

    // The state ties the response to the request of this browser (CSRF), the iss to Authora (RFC 9207).
    if (!pending || parameters.get('state') !== pending.state) {
        throw new Error('The sign-in response does not belong to this browser. Sign in again.');
    }

    if (parameters.get('iss') !== settings.issuer) {
        throw new Error('The sign-in response comes from another issuer. Sign in again.');
    }

    if (parameters.has('error')) {
        throw new Error(parameters.get('error') === 'access_denied'
            ? 'You did not allow the admin interface to manage Authora.'
            : parameters.get('error_description') ?? parameters.get('error'));
    }

    await requestTokens({
        grant_type: 'authorization_code',
        client_id: settings.clientId,
        code: parameters.get('code'),
        redirect_uri: settings.redirectUri,
        code_verifier: pending.verifier
    });

    return true;
}

/**
 * Gets a valid Access Token, refreshing it if it is about to expire.
 * @returns {Promise<string|null>} The Access Token, or null if the User must sign in again.
 */
export async function getAccessToken() {
    const tokens = readTokens();

    if (tokens === null) {
        return null;
    }

    if (Date.now() < tokens.expiresAt - refreshMarginSeconds * 1000) {
        return tokens.accessToken;
    }

    return await refresh();
}

/**
 * Exchanges the Refresh Token for new tokens. Concurrent callers share one request, because a Refresh Token can
 * only be used once: a second use would count as reuse and revoke the whole Grant.
 * @returns {Promise<string|null>} The new Access Token, or null if the User must sign in again.
 */
export function refresh() {
    refreshing ??= (async () => {
        try {
            const tokens = readTokens();

            if (!tokens?.refreshToken) {
                clearTokens();
                return null;
            }

            await requestTokens({
                grant_type: 'refresh_token',
                client_id: settings.clientId,
                refresh_token: tokens.refreshToken
            });

            return readTokens()?.accessToken ?? null;
        } catch {
            clearTokens();
            return null;
        } finally {
            refreshing = null;
        }
    })();

    return refreshing;
}

/**
 * Revokes the Grant, forgets the tokens and opens the sign-out page of Authora, which ends the Session.
 * @returns {Promise<void>} Completes when the page is left.
 */
export async function signOut() {
    const tokens = readTokens();
    clearTokens();

    if (tokens?.refreshToken) {
        // Revoking the Refresh Token revokes the whole Grant, including the Access Token.
        await fetch(`${basePath()}/oauth/revoke`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({
                token: tokens.refreshToken,
                token_type_hint: 'refresh_token',
                client_id: settings.clientId
            })
        }).catch(() => { });
    }

    window.location.assign(`${basePath()}/account/logout`);
}

async function requestTokens(parameters) {
    const response = await fetch(`${basePath()}/oauth/token`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams(parameters)
    });

    const body = await response.json().catch(() => ({}));

    if (!response.ok) {
        throw new Error(body.error_description ?? body.error ?? 'The tokens could not be issued.');
    }

    sessionStorage.setItem(tokenKey, JSON.stringify({
        accessToken: body.access_token,
        refreshToken: body.refresh_token ?? null,
        expiresAt: Date.now() + body.expires_in * 1000
    }));
}

function readTokens() {
    try {
        return JSON.parse(sessionStorage.getItem(tokenKey) ?? 'null');
    } catch {
        return null;
    }
}

function clearTokens() {
    sessionStorage.removeItem(tokenKey);
}

function randomString(length) {
    const bytes = crypto.getRandomValues(new Uint8Array(length));
    return base64Url(bytes).slice(0, length);
}

function base64Url(buffer) {
    const bytes = buffer instanceof Uint8Array ? buffer : new Uint8Array(buffer);
    let binary = '';

    for (const byte of bytes) {
        binary += String.fromCharCode(byte);
    }

    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
