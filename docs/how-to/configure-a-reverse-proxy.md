# Configure a reverse proxy

This guide shows how to run Authora behind a reverse proxy that terminates TLS, such as nginx, Caddy, Traefik or a cloud load balancer.

Without this configuration, Authora sees every request as plain HTTP coming from the proxy's IP address. Then the sign-in fails (its cookies need HTTPS), all users share one rate limit, and the audit log records the proxy's address instead of the user's.

## 1. Configure the proxy

The proxy must:

- forward to Authora's HTTP port (8080 in the container image),
- send `X-Forwarded-Proto` and `X-Forwarded-For`, **overwriting** (not appending to) any `X-Forwarded-For` the client sent,
- forward the original `Host` header.

nginx:

```nginx
server {
    listen 443 ssl;
    server_name auth.example.com;
    # ssl_certificate …; ssl_certificate_key …;

    location / {
        proxy_pass         http://authora:8080;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $remote_addr;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

Caddy sets these headers itself:

```caddy
auth.example.com {
    reverse_proxy authora:8080
}
```

## 2. Tell Authora which proxies to trust

Authora only reads the forwarded headers from the addresses you list, so nobody else can fake their IP address. Set the proxy's address, or the network it runs in:

```sh
Authora__ForwardedHeaders__KnownProxies__0=10.0.0.5
# or
Authora__ForwardedHeaders__KnownNetworks__0=10.0.0.0/24
```

| Setting | Default | Description |
|---|---|---|
| `Authora:ForwardedHeaders:KnownProxies` | `[]` | IP addresses of the proxies. |
| `Authora:ForwardedHeaders:KnownNetworks` | `[]` | Networks of the proxies in CIDR notation. At most `/8` (IPv4) or `/7` (IPv6) broad, so `0.0.0.0/0` cannot make every client a trusted proxy. A network that contains all IPv4-mapped addresses (`::ffff:0:0/96`) is rejected too. |
| `Authora:ForwardedHeaders:ForwardLimit` | `1` | Number of proxies in front of Authora, 1 to 10. |

The headers are only read when one of the two lists is set.

## 3. Set the public URL

```sh
Authora__OAuth__Issuer=https://auth.example.com
AllowedHosts=auth.example.com
```

The Issuer is the URL clients see. Give Authora a dedicated host, e.g. `https://auth.example.com`, without a path. Serving Authora under a path prefix such as `https://example.com/auth` is not supported: an Issuer with a path changes the URLs Authora advertises, but Authora still serves its endpoints at the root, so the advertised URLs return `404`.

## 4. Check it

Sign in at `https://auth.example.com/admin/`, then open the **Audit log**: the `account.sign-in` event must show your own IP address, not the proxy's.

If the sign-in page reports an error about the antiforgery token or the cookie, Authora still thinks the request is plain HTTP: check that the proxy's address matches `KnownProxies` or `KnownNetworks`, as Authora sees it (e.g. the Docker network, not the host).
