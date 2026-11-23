"""Prepares a fresh Authora for the OpenID conformance suite and writes the suite's test plan configurations.

Waits until Authora and the suite answer. Then signs in to the admin interface as the bootstrap administrator, the
way a browser does, and uses the admin API to create the User the suite signs in as and two Confidential Clients (the
suite's "client" and "client2") with the suite's Redirect URI and Post-logout Redirect URI. Both Clients are
first-party, so the suite's browser sees no consent screen, and may omit PKCE, which most tests of the Basic OP plan
do not send.

The Back-Channel Logout OP plan gets a configuration of its own, with another alias and a third Client that has a
Back-Channel Logout URI. Logout Tokens are sent to the alias, i.e. to whichever test of it runs, so the Clients of the
other plans have none: a test of the RP-Initiated Logout plan would fail on a request it does not expect. The URI
points to the TLS proxy "backchannel-proxy" of docker-compose.yml, because Authora cannot verify the certificate of
the suite's nginx (self-signed for "localhost").

Runs in the "runner" container of docker-compose.yml, see run.sh. Settings come from environment variables.
"""

import html
import json
import os
import re
import sys
import time

import httpx

ISSUER = os.environ["AUTHORA_ISSUER"]
ADMIN_EMAIL = os.environ["AUTHORA_ADMIN_EMAIL"]
ADMIN_PASSWORD = os.environ["AUTHORA_ADMIN_PASSWORD"]
USER_EMAIL = os.environ["CONFORMANCE_USER_EMAIL"]
USER_PASSWORD = os.environ["CONFORMANCE_USER_PASSWORD"]
SUITE_URL = os.environ["CONFORMANCE_SERVER"].rstrip("/")
ALIAS = os.environ.get("CONFORMANCE_ALIAS", "authora")
BACKCHANNEL_ALIAS = os.environ.get("CONFORMANCE_BACKCHANNEL_ALIAS", "authora-backchannel")
OUTPUT = os.environ.get("CONFORMANCE_CONFIG", "/work/config.json")
BACKCHANNEL_OUTPUT = os.environ.get("CONFORMANCE_BACKCHANNEL_CONFIG", "/work/config-backchannel.json")
# Forwards to the suite's nginx, with a certificate Authora trusts, see docker-compose.yml.
BACKCHANNEL_PROXY_URL = os.environ.get("CONFORMANCE_BACKCHANNEL_PROXY", "https://backchannel-proxy:8443")


def redirect_uri(alias):
    """The suite sends the browser back to this URL; static Clients must have it registered."""
    return f"{SUITE_URL}/test/a/{alias}/callback"


def post_logout_redirect_uri(alias):
    """The logout tests expect this Post-logout Redirect URI for static Clients."""
    return f"{SUITE_URL}/test/a/{alias}/post_logout_redirect"


def backchannel_logout_uri(alias):
    """The Back-Channel Logout test expects Logout Tokens at this path of the alias."""
    return f"{BACKCHANNEL_PROXY_URL}/test/a/{alias}/backchannel_logout"


def fail(message):
    print(f"setup: {message}", file=sys.stderr)
    sys.exit(1)


def check(response, expected):
    if response.status_code != expected:
        fail(f"{response.request.method} {response.request.url.path} returned {response.status_code}, "
             f"expected {expected}: {response.text[:500]}")
    return response


def wait_for(url, name):
    deadline = time.monotonic() + 300
    while True:
        try:
            if httpx.get(url, verify=False, timeout=10).status_code == 200:
                return
        except httpx.TransportError:
            pass
        if time.monotonic() > deadline:
            fail(f"{name} did not answer at {url} within 5 minutes.")
        time.sleep(2)


def hidden_input(page, name):
    match = re.search(rf'<input[^>]*name="{re.escape(name)}"[^>]*value="([^"]*)"', page)
    if match is None:
        fail(f"The sign-in page has no field {name}.")
    return html.unescape(match.group(1))


def sign_in_to_admin_interface(http):
    # /admin/signin -> /oauth/authorize -> /account/login: the sign-in form.
    login = check(http.get("/admin/signin"), 200)
    if "/account/login" not in login.url.path:
        fail(f"Expected the sign-in page, got {login.url}.")

    form = {
        "Email": ADMIN_EMAIL,
        "Password": ADMIN_PASSWORD,
        "ReturnUrl": hidden_input(login.text, "ReturnUrl"),
        "__RequestVerificationToken": hidden_input(login.text, "__RequestVerificationToken"),
    }

    # Signing in returns to /oauth/authorize, which sends the code to /admin/callback, which sets the token cookie.
    check(http.post(login.url, data=form), 200)
    if not any(cookie.name == "__Host-authora_admin" for cookie in http.cookies.jar):
        fail("Signing in to the admin interface failed; check the administrator's credentials.")


def admin_api(http, method, path, body=None, expected=200):
    # The admin interface's backend turns the cookie into a Bearer token for requests with this header.
    response = http.request(method, f"/api{path}", json=body, headers={"X-Authora-Admin": "1"})
    check(response, expected)
    return response.json() if response.content else None


def register_client(http, name, scope_ids, alias=ALIAS, backchannel_logout=False):
    created = admin_api(http, "POST", "/clients", {
        "name": name,
        "type": "Confidential",
        "redirectUris": [redirect_uri(alias)],
        "postLogoutRedirectUris": [post_logout_redirect_uri(alias)],
        "scopeIds": scope_ids,
    }, expected=201)

    client_id = created["id"]
    if backchannel_logout:
        admin_api(http, "PUT", f"/clients/{client_id}", {
            "backchannelLogoutUri": backchannel_logout_uri(alias),
            "backchannelLogoutSessionRequired": True,
        }, expected=204)
    # Redirect URIs, Post-logout Redirect URIs and Scopes cannot be added and PKCE cannot be made optional any more
    # once a Client is first-party, so this comes last.
    admin_api(http, "PUT", f"/clients/{client_id}/pkce-optional", expected=204)
    admin_api(http, "PUT", f"/clients/{client_id}/first-party", expected=204)

    return {"client_id": client_id, "client_secret": created["secret"]["secret"]}


def browser_automation(screenshot_sign_in=False, logout="redirect"):
    """Tells the suite's headless browser how to sign in and out. First-party Clients get no consent screen.

    Tests that must show that the User was asked to sign in again (prompt=login, max_age) need a screenshot of the
    sign-in page, which the "update-image-placeholder" step provides. The logout tests expect Authora to redirect back
    ("redirect"), to show an error ("error"), or to show that the User is signed out, right away ("signed-out") or
    after the User confirmed ("confirm-signed-out").
    """
    sign_in = [
        ["text", "id", "Email", USER_EMAIL],
        ["text", "id", "Password", USER_PASSWORD],
        ["click", "css", "button[type=submit]"],
    ]
    if screenshot_sign_in:
        sign_in.insert(0, ["wait", "xpath", "//*", 10, "Sign in", "update-image-placeholder-optional"])

    return [
        {
            "match": f"{ISSUER}/oauth/authorize*",
            "tasks": [
                {
                    "task": "Sign in",
                    "optional": True,
                    "match": f"{ISSUER}/account/login*",
                    "commands": sign_in,
                },
                {
                    "task": "Verify complete",
                    "match": "*/test/*/callback*",
                    "commands": [["wait", "id", "submission_complete", 10]],
                },
            ],
        },
        logout_automation(logout),
    ]


def logout_automation(expected):
    """The end-session endpoint: Authora asks for confirmation unless the id_token_hint is of the current Session."""
    confirm = {
        "task": "Confirm signing out",
        "optional": True,
        "match": f"{ISSUER}/oauth/logout*",
        "commands": [["click", "css", "button[value=sign-out]"]],
    }

    if expected == "error":
        tasks = [{
            "task": "Expect the error page",
            "match": f"{ISSUER}/oauth/logout*",
            "commands": [["wait", "xpath", "//*", 10, "The request cannot be processed", "update-image-placeholder"]],
        }]
    elif expected in ("signed-out", "confirm-signed-out"):
        # With the id_token_hint of the Session, Authora signs out right away, without a confirmation page.
        tasks = [confirm] if expected == "confirm-signed-out" else []
        tasks.append({
            "task": "Expect the signed-out page",
            "match": f"{ISSUER}/oauth/logout*",
            "commands": [["wait", "xpath", "//*", 10, "You are signed out", "update-image-placeholder"]],
        })
    else:
        tasks = [confirm, {"task": "Verify complete", "match": "*/test/*/post_logout_redirect*"}]

    return {"match": f"{ISSUER}/oauth/logout*", "tasks": tasks}


def error_page_automation():
    """For tests that expect Authora to show an error instead of redirecting, e.g. an unregistered Redirect URI."""
    return [
        {
            "match": f"{ISSUER}/oauth/authorize*",
            "tasks": [
                {
                    "task": "Expect the error page",
                    "match": f"{ISSUER}/oauth/authorize*",
                    "commands": [
                        ["wait", "xpath", "//*", 10, "The request cannot be processed", "update-image-placeholder"],
                    ],
                }
            ],
        }
    ]


def main():
    wait_for(f"{ISSUER}/.well-known/openid-configuration", "Authora")
    # The plan listing needs the suite's database too, see wait_for_server_ready in the suite's conformance.py.
    wait_for(f"{SUITE_URL}/api/plan?length=1", "The conformance suite")

    with httpx.Client(base_url=ISSUER, verify=False, follow_redirects=True, timeout=30) as http:
        sign_in_to_admin_interface(http)

        admin_api(http, "POST", "/users", {
            "email": USER_EMAIL,
            "firstName": "Conformance",
            "lastName": "Tester",
            "password": USER_PASSWORD,
        }, expected=201)

        scopes = {scope["name"]: scope["id"] for scope in admin_api(http, "GET", "/scopes")}
        scope_ids = [scopes[name] for name in ("openid", "profile", "email")]

        client = register_client(http, "Conformance client 1", scope_ids)

        config = {
            "alias": ALIAS,
            "description": "Authora",
            "server": {"discoveryUrl": f"{ISSUER}/.well-known/openid-configuration"},
            "client": client,
            "client2": register_client(http, "Conformance client 2", scope_ids),
            # The Client for oidcc-server-client-secret-post. Authora accepts both methods from every Client.
            "client_secret_post": client,
            "browser": browser_automation(),
            "override": {
                "oidcc-prompt-login": {"browser": browser_automation(screenshot_sign_in=True)},
                "oidcc-max-age-1": {"browser": browser_automation(screenshot_sign_in=True)},
                "oidcc-ensure-registered-redirect-uri": {"browser": error_page_automation()},
                # Authora rejects the redirect_uri outside the request object before it looks at the request object.
                "oidcc-ensure-request-object-with-redirect-uri": {"browser": error_page_automation()},
                # RP-Initiated Logout plan: invalid requests are shown as errors and never redirected.
                **{test: {"browser": browser_automation(logout="error")} for test in [
                    "oidcc-rp-initiated-logout-bad-id-token-hint",
                    "oidcc-rp-initiated-logout-modified-id-token-hint",
                    "oidcc-rp-initiated-logout-no-id-token-hint",
                    "oidcc-rp-initiated-logout-bad-post-logout-redirect-uri",
                    "oidcc-rp-initiated-logout-query-added-to-post-logout-redirect-uri",
                ]},
                # Without a Post-logout Redirect URI, Authora shows that the User is signed out; without an
                # id_token_hint, the User confirms first.
                **{test: {"browser": browser_automation(logout="confirm-signed-out")} for test in [
                    "oidcc-rp-initiated-logout-no-params",
                    "oidcc-rp-initiated-logout-only-state",
                ]},
                "oidcc-rp-initiated-logout-no-post-logout-redirect-uri": {
                    "browser": browser_automation(logout="signed-out"),
                },
            },
        }

        backchannel_client = register_client(
            http, "Conformance back-channel client", scope_ids, alias=BACKCHANNEL_ALIAS, backchannel_logout=True)

        backchannel_config = {
            "alias": BACKCHANNEL_ALIAS,
            "description": "Authora (back-channel logout)",
            "server": {"discoveryUrl": f"{ISSUER}/.well-known/openid-configuration"},
            "client": backchannel_client,
            "browser": browser_automation(),
        }

    for path, value in ((OUTPUT, config), (BACKCHANNEL_OUTPUT, backchannel_config)):
        with open(path, "w", encoding="utf-8") as file:
            json.dump(value, file, indent=4)

    print(f"setup: created the User {USER_EMAIL} and three Clients, wrote {OUTPUT} and {BACKCHANNEL_OUTPUT}")


if __name__ == "__main__":
    main()
