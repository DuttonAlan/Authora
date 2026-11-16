#!/usr/bin/env bash
# Runs the OpenID Foundation conformance suite's Basic OP certification test plan against Authora built from this
# repository, with Authora's results compared to expected-failures.json and expected-skips.json.
#
# Needs bash, Docker with Compose, curl and OpenSSL. Used by .github/workflows/conformance.yml; locally:
#
#   bash tests/conformance/run.sh               # build, run, clean up; exit code 0 if the results are as expected
#   KEEP_RUNNING=1 bash tests/conformance/run.sh  # leave the stack running to inspect the results in the suite's UI
#
# Output in tests/conformance/.work/: results/ (the suite's export of the plan), logs/ (container logs).
# See docs/how-to/run-the-conformance-tests.md.
set -euo pipefail

# Git Bash on Windows would turn the OpenSSL subject "/CN=…" into a Windows path.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")"

# The suite's images and scripts, from https://gitlab.com/openid/conformance-suite/-/tags
export SUITE_VERSION="${SUITE_VERSION:-release-v5.3.4}"
PLAN='oidcc-basic-certification-test-plan[server_metadata=discovery][client_registration=static_client]'

compose() {
    docker compose --file docker-compose.yml "$@"
}

collect_logs() {
    mkdir -p .work/logs
    for service in authora server nginx; do
        compose logs --no-color "$service" > ".work/logs/$service.log" 2>&1 || true
    done
}

cleanup() {
    collect_logs
    if [ "${KEEP_RUNNING:-0}" = "1" ]; then
        echo "The stack keeps running: suite at https://localhost.emobix.co.uk:8443, Authora at https://authora:9443."
        echo "Stop it with: docker compose --file tests/conformance/docker-compose.yml --profile runner down --volumes"
    else
        compose --profile runner down --volumes --remove-orphans > /dev/null 2>&1 || true
    fi
}

echo "==> Preparing keys and passwords"
compose --profile runner down --volumes --remove-orphans > /dev/null 2>&1 || true
rm -rf .work
mkdir -p .work/secrets .work/results .work/suite

openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out .work/secrets/signing-key.pem 2> /dev/null
openssl req -x509 -newkey rsa:2048 -nodes -days 2 -subj "/CN=authora" -addext "subjectAltName=DNS:authora" \
    -keyout .work/secrets/tls.key -out .work/secrets/tls.crt 2> /dev/null
# Authora runs as a non-root user in the container.
chmod 644 .work/secrets/*

# Only for this run: the stack is removed afterwards.
export AUTHORA_ADMIN_EMAIL="admin@example.com"
export AUTHORA_ADMIN_PASSWORD="admin-$(openssl rand -hex 16)"
export CONFORMANCE_USER_PASSWORD="user-$(openssl rand -hex 16)"

echo "==> Downloading the conformance suite's scripts ($SUITE_VERSION)"
curl --fail --silent --show-error --location \
    "https://gitlab.com/openid/conformance-suite/-/archive/$SUITE_VERSION/conformance-suite-$SUITE_VERSION.tar.gz?path=scripts" \
    | tar --extract --gzip --directory .work/suite --strip-components=1

trap cleanup EXIT

echo "==> Building Authora and starting the stack"
compose build
compose up --detach --quiet-pull db migrate authora mongodb server nginx

echo "==> Running $PLAN"
compose --profile runner run --rm --no-deps runner sh -c '
    set -e
    pip install --quiet --requirement /work/suite/scripts/requirements.txt
    python /conformance/setup.py
    python /work/suite/scripts/run-test-plan.py \
        --verbose \
        --expected-failures-file /conformance/expected-failures.json \
        --expected-skips-file /conformance/expected-skips.json \
        --export-dir /work/results \
        "$0" /work/config.json
' "$PLAN"
