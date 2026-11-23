# Receive back-channel logout

This guide shows how an application learns that a User signed out at Authora, so it can end its own session for that User too, even if the browser never comes back to it. Authora implements [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html): it sends a signed Logout Token from server to server. It assumes the application already signs in with Authora, see [Connect an application](connect-an-application.md).

Authora notifies the application when the Session the User signed in to it with is ended on purpose: the User signs out (at Authora or through any application), changes or resets the password, removes a second factor, or an administrator signs the User out, deactivates or deletes them. A Session that only expires sends nothing. The full list is in [Back-channel logout](../reference/openid-connect.md#back-channel-logout).

## 1. Keep the `sid` of the sign-in

Every ID Token contains `sid`, the Session at Authora the User signed in with. Keep it with your session for the User, e.g. as a claim in the session cookie. The Logout Token carries the same `sid`, so you can end exactly the sessions that belong to the Session that ended.

## 2. Add an endpoint for Logout Tokens

Authora sends `POST` with `Content-Type: application/x-www-form-urlencoded` and one parameter, `logout_token`. Your endpoint must be reachable from Authora's servers (not only from browsers), over HTTPS, and must not require a signed-in user, a cookie or an antiforgery token.

Validate the token like this, and answer `400 Bad Request` if any check fails:

1. It is a JWT signed with `RS256` by a key from Authora's JWKS (`/.well-known/jwks.json`), found by the `kid` of the header.
2. The header has `typ: logout+jwt`. This keeps an ID Token from being accepted as a Logout Token.
3. `iss` is your Issuer, `aud` is your Client Id, and `exp` has not passed (Logout Tokens live two minutes).
4. `events` contains the member `http://schemas.openid.net/event/backchannel-logout`.
5. There is no `nonce`.
6. `sid` (always sent) and `sub` (the User Id) are present.

Then end every local session with that `sid`, and answer `200 OK`. Any `2xx` counts as delivered. Answer quickly, within `Authora:BackchannelLogout:Timeout` (5 seconds by default): end the sessions, don't do slow work in the request.

Authora retries a delivery that fails, first after 10 seconds and then with a growing delay, for up to a day (see [Configuration](../reference/configuration.md#back-channel-logout)). The same logout can therefore arrive more than once, each time with a new `jti`; ending sessions that are already ended must succeed.

## Example: ASP.NET Core

With the setup from [Connect an application](connect-an-application.md#example-asp-net-core) (`MapInboundClaims = false`, so the `sid` claim keeps its name), remember ended Sessions in a distributed cache and reject cookies whose `sid` is in it:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// One instance: in memory. Several instances: a shared cache, e.g. Redis, so every instance sees the logout.
builder.Services.AddDistributedMemoryCache();

builder.Services
    .AddAuthentication(/* as in Connect an application */)
    .AddCookie(options =>
    {
        // Checked on every request: a cookie of a Session that ended at Authora is rejected.
        options.Events.OnValidatePrincipal = async context =>
        {
            var sid = context.Principal?.FindFirst("sid")?.Value;
            var endedSessions = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();

            if (sid is not null && await endedSessions.GetStringAsync($"ended-sid:{sid}") is not null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    })
    .AddOpenIdConnect(options => { /* as in Connect an application */ });

app.MapPost("/backchannel-logout", async (
    HttpContext http,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    IDistributedCache endedSessions) =>
{
    http.Response.Headers.CacheControl = "no-store";

    var form = await http.Request.ReadFormAsync();
    var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
    var authora = await options.ConfigurationManager!.GetConfigurationAsync(http.RequestAborted);

    // Signature, iss, aud, exp and the typ header.
    var result = await new JsonWebTokenHandler().ValidateTokenAsync(form["logout_token"].ToString(), new TokenValidationParameters
    {
        ValidIssuer = authora.Issuer,
        ValidAudience = options.ClientId,
        IssuerSigningKeys = authora.SigningKeys,
        ValidTypes = ["logout+jwt"]
    });

    if (!result.IsValid
        || result.SecurityToken is not JsonWebToken token
        || !token.TryGetPayloadValue<Dictionary<string, object>>("events", out var events)
        || !events.ContainsKey("http://schemas.openid.net/event/backchannel-logout")
        || token.TryGetPayloadValue<string>("nonce", out _)
        || !token.TryGetPayloadValue<string>("sid", out var sid))
    {
        return Results.BadRequest();
    }

    // Longer than your cookie lives, so no cookie of the Session can come back.
    await endedSessions.SetStringAsync($"ended-sid:{sid}", "1", new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14)
    }, http.RequestAborted);

    return Results.Ok();
}).AllowAnonymous().DisableAntiforgery();
```

The handler fetches Authora's discovery document and JWKS itself and caches them, so it also follows a [key rotation](rotate-signing-keys.md).

If the application keeps Access or Refresh Tokens of the User, drop them too: Authora has already revoked them, so they no longer work anyway.

## 3. Register the URI

In the admin interface, open the Client and enter the endpoint under **Edit** → **Back-Channel Logout URI**, e.g. `https://app.example.com/backchannel-logout`. Tick *The Client requires sid in Logout Tokens* if your endpoint relies on `sid`; Authora always sends it, the box only records it. Or with the admin API (Permission `clients.update`):

```sh
curl -X PUT https://auth.example.com/api/clients/$CLIENT_ID \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"backchannelLogoutUri": "https://app.example.com/backchannel-logout", "backchannelLogoutSessionRequired": true}'
```

The URI must be absolute, without a fragment, and use HTTPS. An empty string removes it. In local development, `appsettings.Development.json` allows plain HTTP, e.g. `http://localhost:5000/backchannel-logout`.

## 4. Test it

Sign in to the application, then sign out at Authora (e.g. `https://auth.example.com/account/logout`, or from another application). Within a few seconds, your endpoint receives the Logout Token, and the next request with the old cookie is signed out.

## Troubleshooting

| Symptom | Cause |
|---|---|
| Nothing arrives | No Back-Channel Logout URI on the Client, or the User signed in to the application before the URI was set and the Session only expired. Log events 18011 – 18014 show each delivery; look for the Client Id. |
| Log event 18012 *… failed in attempt …: HTTP 400* | Your endpoint rejected the token. Check the validation, in particular that it expects `typ: logout+jwt`, not `JWT`. |
| Log event 18012 *… The SSL connection could not be established* | Authora does not trust your endpoint's certificate. Use a certificate from a CA in the system's trust store of the Authora host. |
| Log event 18012 *… HTTP 302* | Your endpoint redirects, e.g. to a sign-in page. Authora does not follow redirects; allow anonymous access to it. |
| Audit event `client.backchannel-logout-failed` | Authora gave up after the last attempt. The User may still be signed in to the application until its own session ends. |
