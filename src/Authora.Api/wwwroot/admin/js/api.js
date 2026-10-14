// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

// Calls the admin API with the Access Token. The admin interface uses only these documented endpoints, the same
// ones every other Client uses (US-033); the server checks every Permission itself.

import { basePath, getAccessToken, refresh, signIn } from './auth.js';

/**
 * An error response of the admin API.
 */
export class ApiError extends Error {
    /**
     * @param {number} status The HTTP status code.
     * @param {string} message A message for the User.
     */
    constructor(status, message) {
        super(message);
        this.status = status;
    }
}

/**
 * Calls the admin API.
 * @param {string} method The HTTP method.
 * @param {string} path The path below /api, e.g. "/users".
 * @param {object} [body] The JSON body.
 * @returns {Promise<any>} The parsed response, or null for 204 No Content.
 * @throws {ApiError} If the API answers with an error.
 */
export async function api(method, path, body) {
    let response = await send(method, path, body, await getAccessToken());

    // The token may have been revoked or expired early, e.g. after the User's Roles changed. Try once more.
    if (response?.status === 401) {
        response = await send(method, path, body, await refresh());
    }

    if (response === null || response.status === 401) {
        await signIn();
    }

    if (response.status === 204) {
        return null;
    }

    const content = await response.json().catch(() => null);

    if (!response.ok) {
        throw new ApiError(response.status, describe(response.status, content));
    }

    return content;
}

/**
 * Builds a query string from the parameters that are set.
 * @param {object} parameters The parameters.
 * @returns {string} The query string with "?", or an empty string.
 */
export function query(parameters) {
    const entries = Object.entries(parameters).filter(([, value]) => value !== null && value !== undefined && value !== '');
    return entries.length > 0 ? `?${new URLSearchParams(entries)}` : '';
}

async function send(method, path, body, accessToken) {
    if (!accessToken) {
        return null;
    }

    const headers = { Accept: 'application/json', Authorization: `Bearer ${accessToken}` };

    if (body !== undefined) {
        headers['Content-Type'] = 'application/json';
    }

    return await fetch(`${basePath()}/api${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        // The admin API ignores cookies; do not send the Session cookie along.
        credentials: 'omit'
    });
}

function describe(status, problem) {
    if (status === 403) {
        return 'You do not have the Permission for this action.';
    }

    if (status === 429) {
        return 'Too many requests. Wait a moment and try again.';
    }

    if (problem?.errors) {
        return Object.values(problem.errors).flat().join(' ');
    }

    return problem?.detail ?? problem?.title ?? `The request failed (${status}).`;
}
