// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

// Signs in through the backend for frontend of the admin interface: Authora runs the Authorization Code flow
// with PKCE on the server and keeps the tokens in an encrypted HttpOnly cookie. No script ever sees a token, so a
// script that runs here cannot take the tokens elsewhere.

/**
 * The header the server requires on every call of the admin API and of the session endpoints. A cross-site request
 * cannot send it without a CORS preflight, which Authora never allows for them.
 */
export const adminHeader = { 'X-Authora-Admin': '1' };

const signInErrors = {
    access_denied: 'You did not allow the admin interface to manage Authora.',
    unavailable: 'The admin interface is not available: its Client is disabled or missing.',
    invalid_response: 'The sign-in response does not belong to this browser. Sign in again.',
    sign_in_failed: 'The sign-in failed. Sign in again.'
};

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
 * Sends the browser to the sign-in of the backend for frontend. Returns to the current view afterwards.
 * @returns {Promise<void>} Never completes, because the page is left.
 */
export function signIn() {
    const parameters = new URLSearchParams({ returnTo: window.location.hash || '#/' });

    window.location.assign(`${basePath()}/admin/signin?${parameters}`);

    return new Promise(() => { });
}

/**
 * Takes the error of a failed sign-in from the address, if the page was opened with one.
 * @returns {string|null} A message for the User, or null.
 */
export function takeSignInError() {
    const parameters = new URLSearchParams(window.location.search);
    const error = parameters.get('error');

    if (error === null) {
        return null;
    }

    window.history.replaceState(null, '', `${window.location.pathname}${window.location.hash}`);

    // Only known codes, so a link cannot show its own text in the admin interface.
    return signInErrors[error] ?? signInErrors.sign_in_failed;
}

/**
 * Asks the server to refresh the tokens in the cookie. Concurrent callers share one request, and tabs take turns
 * (Web Locks), because a Refresh Token can only be used once: a second use would count as reuse and revoke the
 * whole Grant. The server skips the refresh if another tab has just done it.
 * @returns {Promise<boolean>} True if the cookie holds valid tokens, false if the User must sign in again.
 */
export function refresh() {
    refreshing ??= (async () => {
        try {
            const request = () => post('/admin/session/refresh');
            const response = navigator.locks
                ? await navigator.locks.request('authora.admin.refresh', request)
                : await request();

            return response.ok;
        } catch {
            return false;
        } finally {
            refreshing = null;
        }
    })();

    return refreshing;
}

/**
 * Revokes the Grant, deletes the cookie and opens the sign-out page of Authora, which ends the Session.
 * @returns {Promise<void>} Completes when the page is left.
 */
export async function signOut() {
    await post('/admin/session/signout').catch(() => { });

    window.location.assign(`${basePath()}/account/logout`);
}

function post(path) {
    return fetch(`${basePath()}${path}`, {
        method: 'POST',
        headers: adminHeader,
        credentials: 'same-origin'
    });
}
