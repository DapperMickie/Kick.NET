# Getting Started

## Requirements

- .NET 9 SDK
- a KICK app with `client_id` and `client_secret`

## Default path: app auth

Create an app session once on startup:

```csharp
using Kick;

var session = KickSdk.CreateAppSession(new KickAppCredentials
{
    ClientId = "<client-id>",
    ClientSecret = "<client-secret>",
});

var kick = session.Client;
```

The session acquires and caches an app access token in memory automatically.

## Read data

```csharp
var categories = await kick.Categories.GetAsync(new GetCategoriesRequest
{
    Query = "rust",
    Page = 1,
});

var channels = await kick.Channels.GetAsync(new GetChannelsRequest
{
    Slugs = ["xqc"],
});
```

## Use builders for mutations

```csharp
var reward = new CreateChannelRewardRequestBuilder()
    .WithTitle("Song Request")
    .WithCost(250)
    .WithDescription("Paste a valid URL.")
    .RequiresUserInput()
    .IsEnabled()
    .Build();

await kick.ChannelRewards.CreateAsync(reward);
```

```csharp
var update = new UpdateChannelRequestBuilder()
    .WithStreamTitle("Grinding ranked")
    .AddCustomTag("competitive")
    .AddCustomTag("english")
    .Build();

await kick.Channels.UpdateAsync(update);
```

## Exceptional path: user auth

If a workflow needs a user token, use the separate user-auth client:

```csharp
var auth = KickSdk.CreateUserAuthClient(new KickUserAuthOptions
{
    ClientId = "<client-id>",
    ClientSecret = "<client-secret>",
    RedirectUri = "https://localhost/callback",
    DefaultScopes = [KickScopes.UserRead, KickScopes.ChannelRead],
});

var login = auth.CreateLoginRequest();
// redirect to login.AuthorizationUri
// then exchange the callback code:
// var userSession = await auth.ExchangeAuthorizationCodeAsync(code, login.CodeVerifier);
```

## Advanced/manual token path

`StaticAccessTokenProvider` and `DelegateAccessTokenProvider` still exist for advanced scenarios, but they are no longer the primary onboarding path.

## Next guides

- [Sample Projects](D:/Projects/Kick.NET/docs/samples.md)
- [Console Examples](D:/Projects/Kick.NET/docs/console-examples.md)
- [OAuth Guide](D:/Projects/Kick.NET/docs/oauth.md)
- [Webhook Guide](D:/Projects/Kick.NET/docs/webhooks.md)
