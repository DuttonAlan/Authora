# Your first application

In this tutorial you register an application at Authora and sign in to it, by hand, with the Authorization Code flow and PKCE: the same steps an OpenID Connect library performs for you. You will see the consent screen, exchange a code for tokens, read the ID Token and the UserInfo endpoint, and watch Authora detect a stolen Refresh Token.

You only need a browser and a terminal with `curl` and `openssl`. It takes about fifteen minutes.

**Before you start:** complete [Getting started](getting-started.md), so Authora runs at `https://localhost:8443` and you can sign in to the admin interface.

## 1. Register the application

Applications that sign in at Authora are called *Clients*. Register one:

1. Open <https://localhost:8443/admin/#/clients> and click **Register Client**.
2. Fill in the form:

   | Field | Value |
   |---|---|
   | Name | `My first app` |
   | Type | `Public – a browser, mobile or desktop app without a Secret` |
   | Grant Types | leave both checked: `Authorization Code` and `Refresh Token` |
   | Redirect URIs | `http://127.0.0.1:8080/callback` |
   | Allowed Scopes | check `openid`, `profile` and `email` |

3. Click **Register Client**.

Authora opens the new Client. Copy its **Client Id** and keep it in your terminal:

```sh
CLIENT_ID=paste-the-client-id-here
```

A few things to note:

- A *Public* Client has no Secret, like a mobile app or a single-page application. It proves that it started the sign-in with PKCE instead, which you set up next.
- The Redirect URI is where Authora sends the browser back. Authora only redirects to registered Redirect URIs, and they must match exactly. Plain HTTP is only allowed for `127.0.0.1` and `[::1]`, so local test apps work.
- *Allowed Scopes* limits what the Client may ask for. `openid` makes this an OpenID Connect sign-in, `profile` releases the name and `email` the Email address.

## 2. Create a PKCE pair

PKCE ties the code Authora issues to the application that asked for it. The application makes up a random secret, the *code verifier*, and sends only its SHA-256 hash, the *code challenge*, with the sign-in request. When it exchanges the code, it sends the verifier, and Authora checks that it matches.

```sh
CODE_VERIFIER=$(openssl rand -base64 48 | tr -d '=+/\n' | cut -c1-64)
CODE_CHALLENGE=$(printf '%s' "$CODE_VERIFIER" | openssl dgst -sha256 -binary | openssl base64 -A | tr '+/' '-_' | tr -d '=')
echo "$CODE_CHALLENGE"
```

## 3. Send the user to Authora

Build the authorization URL:

```sh
echo "https://localhost:8443/oauth/authorize?response_type=code&client_id=$CLIENT_ID&redirect_uri=http%3A%2F%2F127.0.0.1%3A8080%2Fcallback&scope=openid%20profile%20email&state=tutorial-state&nonce=tutorial-nonce&code_challenge=$CODE_CHALLENGE&code_challenge_method=S256"
```

Open the printed URL in the browser where you are signed in to the admin interface.

This time Authora asks: **My first app wants to access your account**. It lists what each Scope releases, and where you will be sent back. `My first app` is a third-party Client, so Authora asks on every sign-in. You did not see this for the admin interface, because that is a first-party Client.

You did not have to sign in again: you already have a Session at Authora from the admin interface. That is single sign-on.

## 4. Get the code

Authora issues a code that is valid for only **one minute**. Prepare the next command first, so you are quick:

```sh
CODE=paste-the-code-here
```

Now click **Allow**.

The browser goes to `http://127.0.0.1:8080/callback?code=…&state=tutorial-state&iss=https%3A%2F%2Flocalhost%3A8443` and shows an error, because no application listens on port 8080. That is fine: copy the value of `code` from the address bar into the command above and run it.

A real application would now check two values in that URL: `state` must be the value it sent, and `iss` must be Authora's Issuer. Both protect against forged responses.

## 5. Exchange the code for tokens

```sh
curl -k https://localhost:8443/oauth/token \
  -d grant_type=authorization_code \
  -d client_id="$CLIENT_ID" \
  -d code="$CODE" \
  -d redirect_uri=http://127.0.0.1:8080/callback \
  -d code_verifier="$CODE_VERIFIER"
```

If the minute has passed, you get `{"error":"invalid_grant", …}`. Go back to step 3 and try again: you still have your Session, so it is only a click.

Otherwise, the answer looks like this:

```json
{
  "access_token": "ozklvvXHJoSeuvQ5X3z9mLdFedOsjX5ORW8VanpmKT0",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "pqIxxPL21_MkKSDLLLLyqR4CL6CnYU-qI5ZeimX8K1Q",
  "scope": "openid profile email",
  "id_token": "eyJhbGciOiJSUzI1NiIsImtpZCI6…"
}
```

Copy the three tokens into your terminal:

```sh
ACCESS_TOKEN=paste-access-token
REFRESH_TOKEN=paste-refresh-token
ID_TOKEN=paste-id-token
```

Try the same `curl` command again. It fails: a code works exactly once.

## 6. Read the ID Token

The ID Token tells the application *who* signed in. It is a JSON Web Token: three Base64url parts separated by dots, the middle one holding the claims. Decode it:

```sh
python3 -c 'import sys,json,base64; p=sys.argv[1].split(".")[1]; print(json.dumps(json.loads(base64.urlsafe_b64decode(p+"="*(-len(p)%4))),indent=2))' "$ID_TOKEN"
```

```json
{
  "sub": "01a1542c-e0b2-7896-b701-241c6b7e3b7b",
  "name": "Authora Administrator",
  "given_name": "Authora",
  "family_name": "Administrator",
  "email": "admin@example.com",
  "email_verified": false,
  "iss": "https://localhost:8443",
  "aud": "01a1542d-905a-7baf-82b2-e2db33513106",
  "iat": 1792413634,
  "exp": 1792413934,
  "auth_time": 1792413634,
  "at_hash": "bG4HA6mGMOFDblaAba54wg",
  "nonce": "tutorial-nonce"
}
```

- `sub` is your User Id. It never changes, so applications use it, not the Email, to recognize a user.
- `aud` is your Client Id: the token was made for your application only.
- `nonce` is the value you sent in step 3, so the application can tell that this token answers its own request.
- The ID Token expires after five minutes (`exp`). It is meant to be checked once, at sign-in.

A real application also verifies the token's signature with Authora's public keys at <https://localhost:8443/.well-known/jwks.json>. OpenID Connect libraries do that for you.

## 7. Call UserInfo

The Access Token is different: it has no readable content. It is a random value that only Authora can look up. Use it to ask Authora about the User:

```sh
curl -k https://localhost:8443/oauth/userinfo -H "Authorization: Bearer $ACCESS_TOKEN"
```

```json
{"sub":"01a1542c-e0b2-7896-b701-241c6b7e3b7b","name":"Authora Administrator","given_name":"Authora","family_name":"Administrator","email":"admin@example.com","email_verified":false}
```

## 8. Refresh the tokens

The Access Token expires after an hour. The Refresh Token gets a new one without bothering the user:

```sh
curl -k https://localhost:8443/oauth/token \
  -d grant_type=refresh_token \
  -d client_id="$CLIENT_ID" \
  -d refresh_token="$REFRESH_TOKEN"
```

You get a new Access Token **and a new Refresh Token**. The old Refresh Token is now used up. Authora *rotates* Refresh Tokens, so a stolen one is only useful until the real application uses it.

## 9. Watch Authora catch a replayed token

Pretend an attacker stole the old Refresh Token and uses it now:

```sh
curl -k https://localhost:8443/oauth/token \
  -d grant_type=refresh_token \
  -d client_id="$CLIENT_ID" \
  -d refresh_token="$REFRESH_TOKEN"
```

```json
{"error":"invalid_grant","error_description":"The Refresh Token is invalid, expired or revoked."}
```

Authora cannot know whether the attacker or the application holds the newer token, so it revokes the whole sign-in, called the *Grant*: every Access and Refresh Token issued from it. Even the first Access Token, which had not expired yet, stops working at once:

```sh
curl -k -i https://localhost:8443/oauth/userinfo -H "Authorization: Bearer $ACCESS_TOKEN"
```

```
HTTP/2 401
www-authenticate: Bearer error="invalid_token"
```

Open the **Audit log** in the admin interface: the event `oauth.refresh-token-reused` records the attack.

## What you have learned

- An application is a *Client* with Redirect URIs and allowed Scopes.
- The Authorization Code flow: the browser brings back a short-lived, single-use code, and the application exchanges it, with the PKCE verifier, for tokens.
- The ID Token says who signed in. The Access Token is opaque and only Authora can check it, which is why revoking it works immediately.
- Refresh Tokens rotate, and replaying an old one revokes the whole Grant.

## Next steps

- [Connect an application](../how-to/connect-an-application.md) shows the settings for a real OpenID Connect library.
- [Protect an API with introspection](../how-to/protect-an-api.md) lets your API check the Access Tokens.
- [Tokens, Grants and Sessions](../explanation/tokens-and-sessions.md) explains the design behind what you just saw.
