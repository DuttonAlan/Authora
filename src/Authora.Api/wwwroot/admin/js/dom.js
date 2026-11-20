// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

// Small helpers to build the page. All text is set as text nodes, never as HTML, so values from the API
// (e.g. a User name) cannot inject markup or scripts.

import { icon } from './icons.js';

/**
 * Creates an element.
 * @param {string} tag The tag name.
 * @param {object} [attributes] Attributes; "class", "text", event handlers as "onClick" etc., booleans set or remove.
 * @param {...(Node|string|number|null|undefined|false|Array)} children Child nodes or text.
 * @returns {HTMLElement} The element.
 */
export function h(tag, attributes = {}, ...children) {
    const element = document.createElement(tag);

    for (const [name, value] of Object.entries(attributes ?? {})) {
        if (value === undefined || value === null || value === false) {
            continue;
        }

        if (name === 'class') {
            element.className = value;
        } else if (name === 'text') {
            element.textContent = value;
        } else if (name.startsWith('on') && typeof value === 'function') {
            element.addEventListener(name.slice(2).toLowerCase(), value);
        } else if (name === 'value') {
            element.value = value;
        } else if (name === 'checked') {
            element.checked = Boolean(value);
        } else if (value === true) {
            element.setAttribute(name, '');
        } else {
            element.setAttribute(name, String(value));
        }
    }

    append(element, children);

    return element;
}

function append(element, children) {
    for (const child of children) {
        if (child === undefined || child === null || child === false) {
            continue;
        }

        if (Array.isArray(child)) {
            append(element, child);
        } else if (child instanceof Node) {
            element.appendChild(child);
        } else {
            element.appendChild(document.createTextNode(String(child)));
        }
    }
}

/**
 * Replaces the content of an element.
 * @param {HTMLElement} element The element.
 * @param {...any} children The new children.
 */
export function replace(element, ...children) {
    element.replaceChildren();
    append(element, children);
}

/**
 * Shows a short message in the corner of the page.
 * @param {string} message The message.
 * @param {'success'|'error'} [kind] The kind of message.
 */
export function toast(message, kind = 'success') {
    const container = document.getElementById('toasts');
    const element = h('div', { class: kind === 'error' ? 'toast error' : 'toast', role: kind === 'error' ? 'alert' : null },
        icon(kind === 'error' ? 'alert' : 'check'),
        h('span', { text: message }));

    container.appendChild(element);
    setTimeout(() => element.remove(), kind === 'error' ? 8000 : 4000);
}

/**
 * Formats a point in time of the API (UTC, ISO 8601) in the time zone of the browser.
 * @param {string|null|undefined} value The point in time.
 * @returns {string} The formatted value, or "–".
 */
export function formatDate(value) {
    return value ? new Date(value).toLocaleString() : '–';
}

const relativeFormat = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });

/**
 * Formats a point in time relative to now, e.g. "5 minutes ago", with the exact time as tooltip.
 * @param {string|null|undefined} value The point in time.
 * @returns {HTMLElement|string} A time element, or "–".
 */
export function relativeTime(value) {
    if (!value) {
        return '–';
    }

    const date = new Date(value);
    const seconds = Math.round((date.getTime() - Date.now()) / 1000);
    const units = [['day', 86400], ['hour', 3600], ['minute', 60]];
    const [unit, size] = units.find(([, length]) => Math.abs(seconds) >= length) ?? ['second', 1];
    const text = Math.abs(seconds) < 45 ? 'just now' : relativeFormat.format(Math.round(seconds / size), unit);

    return h('time', { datetime: date.toISOString(), title: date.toLocaleString(), text });
}

/**
 * The header of a page with a title and actions.
 * @param {string} title The title.
 * @param {string|null} [subtitle] A line below the title.
 * @param {...Node} actions Buttons on the right.
 * @returns {HTMLElement} The header.
 */
export function pageHeader(title, subtitle, ...actions) {
    const visible = actions.filter(Boolean);

    return h('header', { class: 'page-header' },
        h('div', { class: 'page-heading' }, h('h1', { text: title }), subtitle ? h('p', { class: 'subtitle', text: subtitle }) : null),
        visible.length > 0 ? h('div', { class: 'button-row' }, visible) : null);
}

/**
 * The header of the page of a single object: a monogram, its name, its state and its Id.
 * @param {object} options The content.
 * @param {string} options.title The name.
 * @param {string|null} [options.subtitle] A line below the name.
 * @param {string} options.monogram The text of the monogram, one or two letters.
 * @param {string} options.kind What the object is, e.g. "User"; shown above the name.
 * @param {Node|null} [options.status] A badge for its state.
 * @param {string|null} [options.id] The Id, shown with a copy button.
 * @param {Array<Node|null>} [options.actions] Buttons and links on the right.
 * @returns {HTMLElement} The header.
 */
export function entityHeader({ title, subtitle, monogram, kind, status, id, actions = [] }) {
    const visible = actions.filter(Boolean);

    return h('header', { class: 'entity-header' },
        h('div', { class: 'monogram', 'aria-hidden': 'true', text: monogram.slice(0, 2).toUpperCase() }),
        h('div', { class: 'entity-heading' },
            h('p', { class: 'entity-kind', text: kind }),
            h('div', { class: 'entity-title' }, h('h1', { text: title }), status ?? null),
            subtitle ? h('p', { class: 'subtitle', text: subtitle }) : null,
            id ? copyable(id, `${kind} Id`) : null),
        visible.length > 0 ? h('div', { class: 'button-row entity-actions' }, visible) : null);
}

/**
 * The initials of a name for a monogram.
 * @param {...string} words The words, e.g. first and last name.
 * @returns {string} One or two letters.
 */
export function initials(...words) {
    const letters = words.flatMap(word => (word ?? '').split(/[\s._-]+/)).filter(Boolean).map(word => [...word][0]);
    return (letters.length > 1 ? letters[0] + letters[letters.length - 1] : letters[0] ?? '?');
}

/**
 * A panel with a heading.
 * @param {string|null} title The heading.
 * @param {...any} children The content.
 * @returns {HTMLElement} The panel.
 */
export function panel(title, ...children) {
    return h('section', { class: 'panel' }, title ? h('h2', { text: title }) : null, children);
}

/**
 * A short explanation below the heading of a panel.
 * @param {string} text The text.
 * @returns {HTMLElement} The paragraph.
 */
export function hint(text) {
    return h('p', { class: 'hint', text });
}

/**
 * A button.
 * @param {string} label The label.
 * @param {() => void} onClick The click handler.
 * @param {string} [variant] "primary", "danger", "small", "link" or a combination.
 * @returns {HTMLButtonElement} The button.
 */
export function button(label, onClick, variant = '') {
    return h('button', { type: 'button', class: variant || null, onClick }, label);
}

/**
 * A badge, e.g. for a state. Badges with a kind get a dot, so the state is shown by shape and text, not color alone.
 * @param {string} text The text.
 * @param {'ok'|'bad'|'warn'|'neutral'|''} [kind] The color.
 * @returns {HTMLElement} The badge.
 */
export function badge(text, kind = '') {
    return h('span', { class: `badge ${kind ? `status ${kind}` : ''}`.trim(), text });
}

/**
 * A table.
 * @param {Array<{label: string, render: (row: any) => any, class?: string}>} columns The columns.
 * @param {Array<any>} rows The rows.
 * @param {object} [options] Options.
 * @param {(row: any) => string} [options.href] The address a row opens. The first cell becomes a link, so rows can
 *     be reached with the keyboard; a click anywhere else in the row follows it too.
 * @param {string} [options.empty] The text when there are no rows.
 * @returns {HTMLElement} The table.
 */
export function table(columns, rows, { href, empty = 'Nothing to show.' } = {}) {
    if (rows.length === 0) {
        return h('p', { class: 'empty', text: empty });
    }

    const row = item => {
        const target = href ? href(item) : null;
        const cells = columns.map((column, index) => {
            const content = column.render(item);

            return h('td', { class: column.class ?? null },
                index === 0 && target ? h('a', { class: 'row-link', href: target }, content) : content);
        });

        if (target) {
            cells.push(h('td', { class: 'chevron-cell', 'aria-hidden': 'true' }, icon('chevron')));
        }

        return h('tr', {
            class: target ? 'clickable' : null,
            onClick: target ? event => {
                // Controls and links in the row keep their own behavior.
                if (!event.target.closest('a, button, input, select, label')) {
                    window.location.hash = target;
                }
            } : null
        }, cells);
    };

    return h('div', { class: 'table-wrapper' },
        h('table', {},
            h('thead', {}, h('tr', {},
                columns.map(column => h('th', { scope: 'col', class: column.class ?? null, text: column.label })),
                href ? h('th', { class: 'chevron-cell' }, h('span', { class: 'visually-hidden', text: 'Open' })) : null)),
            h('tbody', {}, rows.map(row))));
}

/**
 * Previous and next buttons for a paged list.
 * @param {number} page The current 1-based page.
 * @param {number} pageSize The items per page.
 * @param {number} total The total number of items.
 * @param {(page: number) => void} onChange Called with the new page.
 * @returns {HTMLElement} The pager.
 */
export function pager(page, pageSize, total, onChange) {
    const pages = Math.max(1, Math.ceil(total / pageSize));

    return h('div', { class: 'pager' },
        h('span', { text: `${total.toLocaleString()} item${total === 1 ? '' : 's'} · page ${page} of ${pages}` }),
        pages > 1 ? h('div', { class: 'button-row' },
            h('button', { type: 'button', class: 'small', disabled: page <= 1, onClick: () => onChange(page - 1) }, 'Previous'),
            h('button', { type: 'button', class: 'small', disabled: page >= pages, onClick: () => onChange(page + 1) }, 'Next')) : null);
}

/**
 * A list of terms and descriptions.
 * @param {Array<[string, any]>} entries The terms and their values.
 * @returns {HTMLElement} The list.
 */
export function details(entries) {
    return h('dl', { class: 'details' }, entries.map(([term, value]) => h('div', {}, h('dt', { text: term }), h('dd', {}, value ?? '–'))));
}

/**
 * A form with fields.
 * @param {Array<object>} fields The fields: name, label, type ("text", "email", "password", "number", "textarea",
 *     "select", "checkbox", "checks", "datetime-local"), and optional value, required, options, help, disabled, ...
 * @param {string} submitLabel The label of the submit button.
 * @param {(values: object, form: HTMLFormElement) => Promise<void>} onSubmit Called with the values.
 * @param {object} [options] Options.
 * @param {() => void} [options.onCancel] Shows a Cancel button that calls it.
 * @returns {HTMLFormElement} The form.
 */
export function form(fields, submitLabel, onSubmit, { onCancel } = {}) {
    const submit = h('button', { type: 'submit', class: 'primary' }, submitLabel);

    const element = h('form', { class: 'form', novalidate: false },
        fields.map(field),
        h('div', { class: 'button-row form-actions' },
            onCancel ? h('button', { type: 'button', onClick: onCancel }, 'Cancel') : null,
            submit));

    element.addEventListener('submit', async event => {
        event.preventDefault();
        submit.disabled = true;

        try {
            await onSubmit(readValues(element, fields), element);
        } finally {
            submit.disabled = false;
        }
    });

    return element;
}

function field(definition) {
    const id = `field-${definition.name}-${Math.random().toString(36).slice(2, 8)}`;
    const helpId = definition.help ? `${id}-help` : null;
    const help = definition.help ? h('p', { class: 'help', id: helpId, text: definition.help }) : null;
    const common = {
        id,
        name: definition.name,
        required: definition.required,
        disabled: definition.disabled,
        placeholder: definition.placeholder,
        autocomplete: definition.autocomplete ?? 'off',
        'aria-describedby': helpId
    };
    let input;

    switch (definition.type) {
        case 'textarea':
            input = h('textarea', { ...common, value: definition.value ?? '' });
            break;
        case 'select':
            input = h('select', common, (definition.options ?? []).map(option =>
                h('option', { value: option.value, selected: option.value === definition.value }, option.label)));
            break;
        case 'checkbox':
            return h('div', { class: 'field' },
                h('label', { class: 'check' }, h('input', { ...common, type: 'checkbox', checked: definition.value }), h('span', { text: definition.label })),
                help);
        case 'checks':
            return h('fieldset', { class: 'field' },
                h('legend', { text: definition.label }),
                h('div', { class: 'checks' }, (definition.options ?? []).map(option => h('label', { class: 'check' },
                    h('input', {
                        type: 'checkbox',
                        name: definition.name,
                        value: option.value,
                        checked: (definition.value ?? []).includes(option.value),
                        disabled: definition.disabled
                    }),
                    h('span', { text: option.label })))),
                help);
        default:
            input = h('input', {
                ...common,
                type: definition.type ?? 'text',
                value: definition.value ?? '',
                min: definition.min,
                max: definition.max,
                minlength: definition.minLength,
                maxlength: definition.maxLength
            });
    }

    return h('div', { class: 'field' },
        h('label', { for: id, text: definition.label }),
        input,
        help);
}

function readValues(element, fields) {
    const values = {};

    for (const definition of fields) {
        if (definition.type === 'checks') {
            values[definition.name] = [...element.querySelectorAll(`input[name="${definition.name}"]:checked`)].map(input => input.value);
        } else if (definition.type === 'checkbox') {
            values[definition.name] = element.elements[definition.name].checked;
        } else if (definition.type === 'number') {
            const raw = element.elements[definition.name].value.trim();
            values[definition.name] = raw === '' ? null : Number(raw);
        } else {
            values[definition.name] = element.elements[definition.name].value;
        }
    }

    return values;
}

async function copy(value) {
    try {
        await navigator.clipboard.writeText(value);
        toast('Copied.');
    } catch {
        toast('Copying is not allowed. Select the value and copy it.', 'error');
    }
}

/**
 * An identifier in monospace with a copy button.
 * @param {string} value The value.
 * @param {string} label What the value is, for screen readers, e.g. "Client Id".
 * @returns {HTMLElement} The element.
 */
export function copyable(value, label) {
    return h('span', { class: 'copyable' },
        h('code', { text: value }),
        h('button', { type: 'button', class: 'icon-button', 'aria-label': `Copy ${label}`, title: 'Copy', onClick: () => copy(value) }, icon('copy')));
}

/**
 * Shows a value that is only shown once, e.g. a new Client Secret, with a copy button.
 * @param {string} label What the value is.
 * @param {string} value The value.
 * @returns {HTMLElement} The box.
 */
export function onceOnlyValue(label, value) {
    return h('div', { class: 'notice', role: 'status' },
        icon('alert', 'notice-icon'),
        h('div', { class: 'notice-body' },
            h('p', {}, h('strong', { text: label }), ' – copy it now, it is not shown again.'),
            h('div', { class: 'secret' },
                h('code', { text: value }),
                button('Copy', () => copy(value), 'small'))));
}

/**
 * Asks before a change, in a modal dialog with Cancel and the action.
 * @param {object} options The content.
 * @param {string} options.title The question, e.g. "Delete the Role?".
 * @param {string} options.message What happens.
 * @param {string} options.confirmLabel The label of the button that performs the action.
 * @param {boolean} [options.destructive] Whether the action removes access or data; colors the button.
 * @returns {Promise<boolean>} True if the User chose the action.
 */
export function confirmDialog({ title, message, confirmLabel, destructive = true }) {
    const titleId = `dialog-title-${Math.random().toString(36).slice(2, 8)}`;
    const cancel = h('button', { type: 'button', value: 'cancel' }, 'Cancel');
    const confirm = h('button', { type: 'button', value: 'confirm', class: destructive ? 'danger' : 'primary' }, confirmLabel);
    const dialog = h('dialog', { class: 'dialog', 'aria-labelledby': titleId },
        h('h2', { id: titleId, text: title }),
        h('p', { text: message }),
        h('div', { class: 'button-row dialog-actions' }, cancel, confirm));

    document.body.appendChild(dialog);

    return new Promise(resolve => {
        const close = result => {
            dialog.close();
            dialog.remove();
            resolve(result);
        };

        cancel.addEventListener('click', () => close(false));
        confirm.addEventListener('click', () => close(true));
        // Escape cancels.
        dialog.addEventListener('cancel', event => {
            event.preventDefault();
            close(false);
        });

        dialog.showModal();
        // Cancel has the focus, so Return does not perform an action that cannot be undone.
        cancel.focus();
    });
}
