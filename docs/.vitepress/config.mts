import { posix } from 'node:path'
import { defineConfig } from 'vitepress'

const repository = 'https://github.com/DuttonAlan/Authora'

export default defineConfig({
  title: 'Authora',
  description:
    'A lightweight, self-hosted identity and access management server: OAuth 2.1 authorization server and OpenID Connect provider, built on .NET and PostgreSQL.',
  lang: 'en-US',
  base: '/',
  cleanUrls: true,

  // The guides link to a local Authora at https://localhost:….
  ignoreDeadLinks: 'localhostLinks',

  // docs/README.md is the index on GitHub; on the website it is the documentation overview, index.md is the homepage.
  rewrites: {
    'README.md': 'overview.md',
  },

  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo.svg' }],
    ['meta', { name: 'theme-color', content: '#3451b2' }],
  ],

  markdown: {
    config(md) {
      // Links that leave docs/ (CHANGELOG.md, SECURITY.md, Dockerfile, …) point to the file on GitHub.
      const linkOpen = md.renderer.rules.link_open
      md.renderer.rules.link_open = (tokens, idx, options, env, self) => {
        const href = tokens[idx].attrGet('href')
        if (href?.startsWith('../')) {
          const target = posix.normalize(posix.join('docs', posix.dirname(env.relativePath), href))
          if (!target.startsWith('docs/')) {
            tokens[idx].attrSet('href', `${repository}/blob/main/${target}`)
          }
        }
        return linkOpen ? linkOpen(tokens, idx, options, env, self) : self.renderToken(tokens, idx, options)
      }

      // Mermaid diagrams are rendered in the browser by the Mermaid component (theme/Mermaid.vue).
      const fence = md.renderer.rules.fence!
      md.renderer.rules.fence = (tokens, idx, options, env, self) => {
        const token = tokens[idx]
        if (token.info.trim() === 'mermaid') {
          return `<Mermaid code="${encodeURIComponent(token.content)}" />`
        }
        return fence(tokens, idx, options, env, self)
      }
    },
  },

  themeConfig: {
    logo: '/logo.svg',

    nav: [
      { text: 'Documentation', link: '/overview', activeMatch: '^/(overview|tutorials|how-to|reference|explanation)/?' },
      { text: 'Getting started', link: '/tutorials/getting-started' },
      { text: 'Changelog', link: `${repository}/blob/main/CHANGELOG.md` },
    ],

    sidebar: [
      { text: 'Overview', link: '/overview' },
      {
        text: 'Tutorials',
        items: [
          { text: 'Getting started', link: '/tutorials/getting-started' },
          { text: 'Your first application', link: '/tutorials/first-application' },
        ],
      },
      {
        text: 'How-to: running Authora',
        items: [
          { text: 'Deploy to production', link: '/how-to/deploy-to-production' },
          { text: 'Configure a reverse proxy', link: '/how-to/configure-a-reverse-proxy' },
          { text: 'Apply migrations and upgrade', link: '/how-to/upgrade-authora' },
          { text: 'Persist Data Protection keys', link: '/how-to/persist-data-protection-keys' },
          { text: 'Rotate signing keys', link: '/how-to/rotate-signing-keys' },
          { text: 'Back up and restore', link: '/how-to/back-up-and-restore' },
          { text: 'Run Authora from source', link: '/how-to/run-from-source' },
        ],
      },
      {
        text: 'How-to: using Authora',
        items: [
          { text: 'Connect an application', link: '/how-to/connect-an-application' },
          { text: 'Protect an API', link: '/how-to/protect-an-api' },
          { text: 'Manage users and access', link: '/how-to/manage-users-and-access' },
          { text: 'Call the admin API', link: '/how-to/call-the-admin-api' },
          { text: 'Check the audit log', link: '/how-to/verify-the-audit-log' },
        ],
      },
      {
        text: 'Reference',
        items: [
          { text: 'Configuration', link: '/reference/configuration' },
          { text: 'OAuth endpoints', link: '/reference/oauth-endpoints' },
          { text: 'OpenID Connect', link: '/reference/openid-connect' },
          { text: 'Admin API', link: '/reference/admin-api' },
          { text: 'Audit events', link: '/reference/audit-events' },
          { text: 'HTTP security', link: '/reference/http-security' },
          { text: 'Health checks', link: '/reference/health-checks' },
          { text: 'Log events', link: '/reference/log-events' },
          { text: 'Standards and limitations', link: '/reference/standards' },
        ],
      },
      {
        text: 'Explanation',
        items: [
          { text: 'Architecture', link: '/explanation/architecture' },
          { text: 'The access model', link: '/explanation/access-model' },
          { text: 'Tokens, Grants and Sessions', link: '/explanation/tokens-and-sessions' },
          { text: 'Security model', link: '/explanation/security-model' },
          { text: 'The admin interface', link: '/explanation/admin-interface' },
          { text: 'Audit log integrity', link: '/explanation/audit-log-integrity' },
        ],
      },
    ],

    socialLinks: [{ icon: 'github', link: repository }],

    search: { provider: 'local' },

    editLink: {
      pattern: `${repository}/edit/main/docs/:path`,
      text: 'Edit this page on GitHub',
    },

    outline: { level: [2, 3] },

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © Alan Dutton',
    },
  },
})
