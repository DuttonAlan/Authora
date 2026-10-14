// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

import { h, toast } from '../dom.js';

/**
 * Runs a change, shows whether it succeeded and shows the current state afterwards.
 * @param {() => Promise<any>} action The change.
 * @param {string} successMessage The message shown after success.
 * @param {object} [context] The context of the view; if given, the view is shown again.
 * @returns {Promise<any>} The result of the action, or undefined if it failed.
 */
export async function run(action, successMessage, context) {
    try {
        const result = await action();
        toast(successMessage);

        if (context) {
            await context.reload();
        }

        return result;
    } catch (error) {
        toast(error.message, 'error');
        return undefined;
    }
}

/**
 * Asks before a change that cannot be undone.
 * @param {string} message The question.
 * @returns {boolean} True if the User agreed.
 */
export function confirmed(message) {
    return window.confirm(message);
}

/**
 * A link to the audit log, filtered by the object of a page.
 * @param {object} context The context of the view.
 * @param {string} targetType The kind of object, e.g. "user".
 * @param {string} targetId The Id of the object.
 * @returns {HTMLElement|null} The link, or null without the Permission to read the audit log.
 */
export function auditLink(context, targetType, targetId) {
    if (!context.can('audit.read')) {
        return null;
    }

    const parameters = new URLSearchParams({ targetType, targetId });
    return h('a', { href: `#/audit?${parameters}` }, 'Show audit log');
}

/**
 * Converts an empty text to null, e.g. for optional fields.
 * @param {string} value The text.
 * @returns {string|null} The trimmed text, or null if it is empty.
 */
export function emptyToNull(value) {
    const trimmed = (value ?? '').trim();
    return trimmed === '' ? null : trimmed;
}
