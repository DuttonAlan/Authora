// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

// Line icons on a 24 px grid with a 1.75 px stroke, so they match the weight of the text next to them. They are
// built as DOM nodes, because the Content Security Policy allows neither inline markup nor inline styles.

const paths = {
    overview: ['M4 13h6V4H4z', 'M14 20h6v-9h-6z', 'M14 7h6V4h-6z', 'M4 20h6v-3H4z'],
    users: ['M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8z', 'M4.5 20a7.5 7.5 0 0 1 15 0'],
    groups: ['M9 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7z', 'M2.5 20a6.5 6.5 0 0 1 13 0', 'M15.5 4.3a3.5 3.5 0 0 1 0 6.4', 'M18 14.2a6.5 6.5 0 0 1 3.5 5.8'],
    roles: ['M8.5 20a4.5 4.5 0 1 0 0-9 4.5 4.5 0 0 0 0 9z', 'M11.7 12.3 20 4', 'M17 7l3 3', 'M14.5 9.5 16.5 11.5'],
    clients: ['M4 5h16a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z', 'M3 9.5h18', 'M6.5 7.25h.01', 'M9 7.25h.01'],
    scopes: ['M3.5 12.6V4.5a1 1 0 0 1 1-1h8.1l8 8a1 1 0 0 1 0 1.4l-7.7 7.7a1 1 0 0 1-1.4 0z', 'M8.5 8.5h.01'],
    audit: ['M9.5 14.5a3.5 3.5 0 0 0 5 0l3.5-3.5a3.5 3.5 0 0 0-5-5l-1 1', 'M14.5 9.5a3.5 3.5 0 0 0-5 0L6 13a3.5 3.5 0 0 0 5 5l1-1'],
    shield: ['M12 3 5 5.8v5.5c0 4.4 3 8.2 7 9.7 4-1.5 7-5.3 7-9.7V5.8z', 'm9 12 2 2 4-4'],
    signOut: ['M14 4h4a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-4', 'M10 16.5 5.5 12 10 7.5', 'M5.5 12H15'],
    menu: ['M4 7h16', 'M4 12h16', 'M4 17h16'],
    close: ['M6 6l12 12', 'M18 6 6 18'],
    copy: ['M9 9h10a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V10a1 1 0 0 1 1-1z', 'M5 15H4.5a1 1 0 0 1-1-1V4.5a1 1 0 0 1 1-1H14a1 1 0 0 1 1 1V5'],
    check: ['M5 12.5 10 17.5 19.5 7'],
    chevron: ['M9.5 6l6 6-6 6'],
    alert: ['M12 4 2.8 19.5h18.4z', 'M12 10v4', 'M12 17h.01'],
    plus: ['M12 5v14', 'M5 12h14'],
    search: ['M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14z', 'M20 20l-4-4'],
    filter: ['M4 6h16', 'M7 12h10', 'M10 18h4']
};

const namespace = 'http://www.w3.org/2000/svg';

/**
 * Creates an icon. Icons are decorative: the control or text next to them carries the meaning.
 * @param {keyof paths} name The name of the icon.
 * @param {string} [className] Additional classes.
 * @returns {SVGSVGElement} The icon.
 */
export function icon(name, className = '') {
    const svg = document.createElementNS(namespace, 'svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('class', `icon ${className}`.trim());
    svg.setAttribute('aria-hidden', 'true');
    svg.setAttribute('focusable', 'false');

    for (const d of paths[name] ?? []) {
        const path = document.createElementNS(namespace, 'path');
        path.setAttribute('d', d);
        svg.appendChild(path);
    }

    return svg;
}
