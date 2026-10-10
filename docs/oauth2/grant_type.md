A grant type is used when you call the /token api endpoint. It specifies on how you get it. 

For example "refresh_token" when you have a refresh token and want a new access token, or "code" if you authorized fresh by the /authorize endpoint.

In my application I want "code" and "refresh_token"