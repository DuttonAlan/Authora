// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

// Small helpers to build the page. All text is set as text nodes, never as HTML, so values from the API
// (e.g. a User name) cannot inject markup or scripts.

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
    const element = h('div', { class: kind === 'error' ? 'toast error' : 'toast', role: kind === 'error' ? 'alert' : null }, message);

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

/**
 * The header of a page with a title and actions.
 * @param {string} title The title.
 * @param {string|null} [subtitle] A line below the title.
 * @param {...Node} actions Buttons on the right.
 * @returns {HTMLElement} The header.
 */
export function pageHeader(title, subtitle, ...actions) {
    return h('div', { class: 'page-header' },
        h('div', {}, h('h1', { text: title }), subtitle ? h('p', { class: 'subtitle', text: subtitle }) : null),
        actions.length > 0 ? h('div', { class: 'button-row' }, actions) : null);
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
 * A button.
 * @param {string} label The label.
 * @param {() => void} onClick The click handler.
 * @param {string} [variant] "primary", "danger", "small", "link" or a combination.
 * @returns {HTMLButtonElement} The button.
 */
export function button(label, onClick, variant = '') {
    return h('button', { type: 'button', class: variant, onClick }, label);
}

/**
 * A badge, e.g. for a state.
 * @param {string} text The text.
 * @param {'ok'|'bad'|''} [kind] The color.
 * @returns {HTMLElement} The badge.
 */
export function badge(text, kind = '') {
    return h('span', { class: `badge ${kind}`.trim(), text });
}

/**
 * A table.
 * @param {Array<{label: string, render: (row: any) => any}>} columns The columns.
 * @param {Array<any>} rows The rows.
 * @param {object} [options] Options.
 * @param {(row: any) => void} [options.onClick] Called when a row is clicked.
 * @param {string} [options.empty] The text when there are no rows.
 * @returns {HTMLElement} The table.
 */
export function table(columns, rows, { onClick, empty = 'Nothing to show.' } = {}) {
    if (rows.length === 0) {
        return h('p', { class: 'empty', text: empty });
    }

    return h('div', { class: 'table-wrapper' },
        h('table', {},
            h('thead', {}, h('tr', {}, columns.map(column => h('th', { scope: 'col', text: column.label })))),
            h('tbody', {}, rows.map(row => h('tr', {
                class: onClick ? 'clickable' : null,
                onClick: onClick ? () => onClick(row) : null
            }, columns.map(column => h('td', { class: column.class ?? null }, column.render(row))))))));
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
        h('span', { text: `${total} item${total === 1 ? '' : 's'} · page ${page} of ${pages}` }),
        h('div', { class: 'button-row' },
            h('button', { type: 'button', class: 'small', disabled: page <= 1, onClick: () => onChange(page - 1) }, 'Previous'),
            h('button', { type: 'button', class: 'small', disabled: page >= pages, onClick: () => onChange(page + 1) }, 'Next')));
}

/**
 * A list of terms and descriptions.
 * @param {Array<[string, any]>} entries The terms and their values.
 * @returns {HTMLElement} The list.
 */
export function details(entries) {
    return h('dl', { class: 'details' }, entries.flatMap(([term, value]) => [h('dt', { text: term }), h('dd', {}, value ?? '–')]));
}

/**
 * A form with fields.
 * @param {Array<object>} fields The fields: name, label, type ("text", "email", "password", "number", "textarea",
 *     "select", "checkbox", "checks", "datetime-local"), and optional value, required, options, help, disabled, ...
 * @param {string} submitLabel The label of the submit button.
 * @param {(values: object, form: HTMLFormElement) => Promise<void>} onSubmit Called with the values.
 * @returns {HTMLFormElement} The form.
 */
export function form(fields, submitLabel, onSubmit) {
    const submit = h('button', { type: 'submit', class: 'primary' }, submitLabel);

    const element = h('form', { class: 'form', novalidate: false },
        fields.map(field),
        h('div', { class: 'button-row' }, submit));

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
    const common = {
        id,
        name: definition.name,
        required: definition.required,
        disabled: definition.disabled,
        placeholder: definition.placeholder,
        autocomplete: definition.autocomplete ?? 'off'
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
                h('label', {}, h('input', { ...common, type: 'checkbox', checked: definition.value }), ' ', definition.label),
                definition.help ? h('p', { class: 'help', text: definition.help }) : null);
        case 'checks':
            return h('fieldset', { class: 'field' },
                h('legend', { text: definition.label }),
                h('div', { class: 'checks' }, (definition.options ?? []).map(option => h('label', {},
                    h('input', {
                        type: 'checkbox',
                        name: definition.name,
                        value: option.value,
                        checked: (definition.value ?? []).includes(option.value),
                        disabled: definition.disabled
                    }),
                    option.label))),
                definition.help ? h('p', { class: 'help', text: definition.help }) : null);
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
        definition.help ? h('p', { class: 'help', text: definition.help }) : null);
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

/**
 * Shows a value that is only shown once, e.g. a new Client Secret, with a copy button.
 * @param {string} label What the value is.
 * @param {string} value The value.
 * @returns {HTMLElement} The box.
 */
export function onceOnlyValue(label, value) {
    return h('div', { class: 'notice' },
        h('p', {}, h('strong', { text: label }), ' – copy it now, it is not shown again.'),
        h('div', { class: 'secret' },
            h('code', { text: value }),
            button('Copy', async () => {
                try {
                    await navigator.clipboard.writeText(value);
                    toast('Copied.');
                } catch {
                    toast('Copying is not allowed. Select the value and copy it.', 'error');
                }
            }, 'small')));
}
