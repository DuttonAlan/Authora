// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

// The WebAuthn calls of the passkey forms. A form marked with data-passkey="create" registers a passkey, one with
// data-passkey="get" signs in with one. On submit, the script asks the page for the options
// (POST ?handler=<data-passkey-options>, with the fields of the form and its antiforgery token), lets the browser
// create or use the passkey, puts the response as JSON into the field marked data-passkey-response, and submits the
// form. The server checks everything; the script only carries the data. No inline scripts or styles (CSP).

const messages = {
  cancelled: 'The passkey request was cancelled or timed out. Try again.',
  duplicate: 'This passkey is already registered.',
  failed: 'The passkey could not be used. Try again, or reload the page.',
  expired: 'This page is out of date. Reload it and try again.'
};

function toBytes(base64url) {
  const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(base64 + '='.repeat((4 - (base64.length % 4)) % 4));
  return Uint8Array.from(binary, (character) => character.charCodeAt(0));
}

function toBase64Url(buffer) {
  const bytes = new Uint8Array(buffer);
  let binary = '';
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

// WebAuthn dictionaries reject null where they expect a value, so empty members are left out.
function withoutNulls(value) {
  if (Array.isArray(value)) {
    return value.map(withoutNulls);
  }
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value).filter(([, member]) => member !== null).map(([key, member]) => [key, withoutNulls(member)]));
  }
  return value;
}

function decodeCredentials(credentials) {
  return (credentials || []).map((credential) => ({ ...credential, id: toBytes(credential.id) }));
}

function creationOptions(options) {
  return {
    ...options,
    challenge: toBytes(options.challenge),
    user: { ...options.user, id: toBytes(options.user.id) },
    excludeCredentials: decodeCredentials(options.excludeCredentials)
  };
}

function requestOptions(options) {
  return {
    ...options,
    challenge: toBytes(options.challenge),
    allowCredentials: decodeCredentials(options.allowCredentials)
  };
}

function encodeCredential(credential) {
  const result = credential.response;
  const response = { clientDataJSON: toBase64Url(result.clientDataJSON) };

  if (result.attestationObject) {
    response.attestationObject = toBase64Url(result.attestationObject);
    response.transports = typeof result.getTransports === 'function' ? result.getTransports() : [];
  }

  if (result.authenticatorData) {
    response.authenticatorData = toBase64Url(result.authenticatorData);
    response.signature = toBase64Url(result.signature);
    if (result.userHandle) {
      response.userHandle = toBase64Url(result.userHandle);
    }
  }

  return {
    id: credential.id,
    rawId: toBase64Url(credential.rawId),
    type: credential.type,
    response,
    clientExtensionResults: typeof credential.getClientExtensionResults === 'function' ? credential.getClientExtensionResults() : {}
  };
}

async function fetchOptions(form) {
  const url = new URL(form.getAttribute('action') || window.location.href, window.location.href);
  url.searchParams.set('handler', form.dataset.passkeyOptions);

  const response = await fetch(url, {
    method: 'POST',
    body: new FormData(form),
    credentials: 'same-origin',
    headers: { Accept: 'application/json' }
  });

  if (response.status === 404 || response.status === 409) {
    throw new Error(messages.expired);
  }

  if (!response.ok) {
    throw new Error(messages.failed);
  }

  return withoutNulls(await response.json());
}

function describe(error, ceremony) {
  if (error instanceof DOMException) {
    if (error.name === 'NotAllowedError' || error.name === 'AbortError') {
      return messages.cancelled;
    }
    if (error.name === 'InvalidStateError' && ceremony === 'create') {
      return messages.duplicate;
    }
    return messages.failed;
  }
  return error instanceof Error && Object.values(messages).includes(error.message) ? error.message : messages.failed;
}

function showError(form, message) {
  const output = form.querySelector('[data-passkey-error]');
  if (output) {
    output.textContent = message;
    output.hidden = false;
  }
}

function setBusy(form, busy) {
  for (const button of form.querySelectorAll('button')) {
    button.disabled = busy;
  }
}

async function run(form) {
  const ceremony = form.dataset.passkey;
  const field = form.querySelector('[data-passkey-response]');

  setBusy(form, true);

  try {
    const options = await fetchOptions(form);
    const credential = ceremony === 'create'
      ? await navigator.credentials.create({ publicKey: creationOptions(options) })
      : await navigator.credentials.get({ publicKey: requestOptions(options) });

    field.value = JSON.stringify(encodeCredential(credential));
    form.submit();
  } catch (error) {
    setBusy(form, false);
    showError(form, describe(error, ceremony));
  }
}

function init() {
  const supported = typeof window.PublicKeyCredential === 'function' && !!navigator.credentials;

  for (const form of document.querySelectorAll('form[data-passkey]')) {
    if (!supported) {
      for (const hint of form.querySelectorAll('[data-passkey-unsupported]')) {
        hint.hidden = false;
      }
      continue;
    }

    for (const button of form.querySelectorAll('button[data-passkey-start]')) {
      button.disabled = false;
    }

    form.addEventListener('submit', (event) => {
      const field = form.querySelector('[data-passkey-response]');
      if (field && field.value) {
        return;
      }
      event.preventDefault();
      run(form);
    });
  }
}

init();
