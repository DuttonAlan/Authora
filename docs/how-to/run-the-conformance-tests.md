# Run the conformance tests

Authora is tested with the [OpenID Foundation conformance suite](https://gitlab.com/openid/conformance-suite), the tool the OpenID Foundation uses for certification. The workflow `.github/workflows/conformance.yml` runs its **Basic OP** and **RP-Initiated Logout OP** test plans against the container built from every pull request and every commit to `main`. This guide shows how to run it yourself, read the results and update them after a change.

## How it works

`tests/conformance/run.sh` does everything:

1. It creates a signing key and a self-signed TLS certificate for the host name `authora`, and random passwords, in `tests/conformance/.work/`.
2. It downloads the suite's test scripts in the version set by `SUITE_VERSION` in `run.sh`, and starts `tests/conformance/docker-compose.yml`: Authora built from your working tree with PostgreSQL, and the suite's published containers (server, nginx, MongoDB) in the same version.
3. `tests/conformance/setup.py` signs in to the admin interface as the bootstrap administrator and creates, through the admin API, the User the suite signs in as and two Confidential Clients with the suite's Redirect URI and Post-logout Redirect URI. Both are first-party, so the suite's browser sees no consent screen, and [PKCE is optional](../reference/oauth-endpoints.md#pkce) for them, because the Basic OP tests do not send it.
4. The suite's `run-test-plan.py` runs `oidcc-basic-certification-test-plan` (discovery, static Clients) and `oidcc-rp-initiated-logout-certification-test-plan` (`response_type=code`, static Clients). The suite's headless browser signs in on Authora's sign-in page and confirms signing out, following the `browser` steps that `setup.py` writes into the configuration. Tests that must show a page to a person (the sign-in page for `prompt=login` and `max_age`, error pages for invalid requests, the signed-out page) capture it and end with the result *Review*.
5. The results are compared with `tests/conformance/expected-failures.json` and `expected-skips.json`. The run fails if a test fails, warns or is skipped that is not listed there, or if a listed one no longer does.

Authora runs with production settings, except that the sign-in and token rate limits are raised, because the suite signs in dozens of times from one IP address.

## Run it

You need bash, Docker with Compose, curl and OpenSSL. Ports 8443 and 9443 must be free.

```sh
bash tests/conformance/run.sh
```

The first run takes several minutes to build Authora's images and download the suite's; the tests themselves take about two minutes, as the suite runs them one after another. The exit code is `0` if the results match the expected ones.

To look at the results in the suite's web interface, keep the containers running:

```sh
KEEP_RUNNING=1 bash tests/conformance/run.sh
```

Then open <https://localhost.emobix.co.uk:8443/> (the name resolves to `127.0.0.1`; accept the self-signed certificate) and choose **View all logs**. Stop the containers with:

```sh
docker compose --file tests/conformance/docker-compose.yml --profile runner down --volumes
```

## Read the results

| Where | What |
|---|---|
| Output of `run.sh` | One line per test with its result, then the unexpected failures, warnings and skips. |
| `tests/conformance/.work/results/` | The suite's export of the plan: the log of every test as HTML and JSON. |
| `tests/conformance/.work/logs/` | Logs of Authora, the suite and nginx. |

In CI, both directories are attached to the workflow run as the artifact `conformance-results`. They contain the passwords and Client Secrets of the run, which only existed in the throwaway containers.

## Update the expected results

The expected results document every check Authora does not pass, with the reason in `comment`. When a change makes a listed check pass, remove its entry. When a change makes a check fail or warn on purpose, add an entry:

```json
{
    "test-name": "oidcc-userinfo-post-body",
    "variant": "*",
    "configuration-filename": "*",
    "current-block": "",
    "condition": "UserInfoEndpointWithAccessTokenInBodyNotSupported",
    "expected-result": "warning",
    "comment": "Authora only accepts the Access Token in the Authorization header."
}
```

`run.sh` prints such an entry for every unexpected result. Each test name, condition and block also appears in the HTML export. Skipped tests go into `expected-skips.json` with `test-name`, `variant`, `configuration-filename` and `comment`.

## Update the suite

Set `SUITE_VERSION` in `tests/conformance/run.sh` to a newer [release tag](https://gitlab.com/openid/conformance-suite/-/tags), run the tests and update the expected results. New suite versions add checks, so expect changes.
