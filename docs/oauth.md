# OAuth Guide

The SDK supports two auth paths:

- default app auth via `client_id` + `client_secret`
- exceptional user auth via browser login and callback exchange

## App auth

This is the default startup path:

```csharp
var session = KickSdk.CreateAppSession(new KickAppCredentials
{
    ClientId = "<client-id>",
    ClientSecret = "<client-secret>",
});

var kick = session.Client;
```

The SDK acquires an app access token with `grant_type=client_credentials` and caches it in memory.

## User auth

Create the user-auth client:

```csharp
var auth = KickSdk.CreateUserAuthClient(new KickUserAuthOptions
{
    ClientId = "<client-id>",
    ClientSecret = "<client-secret>",
    RedirectUri = "https://localhost/callback",
    DefaultScopes = [KickScopes.UserRead, KickScopes.ChannelRead],
});
```

## Create the login request

```csharp
var login = auth.CreateLoginRequest();

Console.WriteLine(login.AuthorizationUri);
Console.WriteLine(login.CodeVerifier);
```

## Exchange the callback code

```csharp
var session = await auth.ExchangeAuthorizationCodeAsync(
    code: "<authorization-code>",
    codeVerifier: login.CodeVerifier);

var kick = session.Client;
```

## Refresh a user session

```csharp
var refreshed = await session.RefreshAsync();
```

## Revoke a user session

```csharp
await session.RevokeAsync();
```

## Low-level OAuth client

`KickOAuthClient` remains available as a lower-level helper for advanced scenarios, but `KickAppSession` and `KickUserAuthClient` are the intended high-level APIs.

## Scope constants

- `KickScopes.UserRead`
- `KickScopes.ChannelRead`
- `KickScopes.ChannelWrite`
- `KickScopes.ChannelRewardsRead`
- `KickScopes.ChannelRewardsWrite`
- `KickScopes.ChatWrite`
- `KickScopes.EventsSubscribe`
- `KickScopes.KicksRead`
- `KickScopes.ModerationBan`
- `KickScopes.ModerationChatMessageManage`
- `KickScopes.StreamKeyRead`
