# Kick .NET SDK

Typed .NET SDK for the KICK public API, with first-class app-auth sessions, user-auth sessions, request builders, and webhook verification.

## Default auth model

The default SDK path is now:

1. configure `client_id` and `client_secret`
2. create an app session
3. use `session.Client` for normal API calls

```csharp
using Kick;

var session = KickSdk.CreateAppSession(new KickAppCredentials
{
    ClientId = "<client-id>",
    ClientSecret = "<client-secret>",
});

var kick = session.Client;
var channels = await kick.Channels.GetAsync(new GetChannelsRequest
{
    Slugs = ["xqc"],
});
```

The session acquires an app access token in memory and reuses it automatically.

## User auth

User auth remains available, but it is a separate path:

```csharp
var auth = KickSdk.CreateUserAuthClient(new KickUserAuthOptions
{
    ClientId = "<client-id>",
    ClientSecret = "<client-secret>",
    RedirectUri = "https://localhost/callback",
    DefaultScopes = [KickScopes.UserRead, KickScopes.ChannelRead],
});

var login = auth.CreateLoginRequest();
// redirect browser to login.AuthorizationUri
// then exchange the returned code:
// var session = await auth.ExchangeAuthorizationCodeAsync(code, login.CodeVerifier);
```

## Projects

- `src/KickNet`: the SDK package
- `samples/KickNet.ConsoleExamples`: general command-based SDK reference
- `samples/KickNet.BotLoginWebhookSample`: minimal API sample for user login and webhook handling
- `samples/KickNet.RewardOpsSample`: reward and redemption workflows
- `samples/KickNet.ChatOpsSample`: chat and moderation workflows
- `samples/KickNet.ChannelDashboardSample`: channel inspection and metadata updates
- `tests/KickNet.Tests`: test suite
- `docs`: usage guides

## More samples

```bash
dotnet run --project samples/KickNet.ConsoleExamples -- help
dotnet run --project samples/KickNet.RewardOpsSample -- help
dotnet run --project samples/KickNet.ChatOpsSample -- help
dotnet run --project samples/KickNet.ChannelDashboardSample
dotnet run --project samples/KickNet.BotLoginWebhookSample
```

## Documentation

- [Getting Started](D:/Projects/Kick.NET/docs/getting-started.md)
- [Sample Projects](D:/Projects/Kick.NET/docs/samples.md)
- [Console Examples](D:/Projects/Kick.NET/docs/console-examples.md)
- [Bot Login + Webhook Sample](D:/Projects/Kick.NET/docs/bot-login-webhook-sample.md)
- [OAuth Guide](D:/Projects/Kick.NET/docs/oauth.md)
- [Webhook Guide](D:/Projects/Kick.NET/docs/webhooks.md)

## Legacy token providers

`IKickAccessTokenProvider`, `StaticAccessTokenProvider`, and `DelegateAccessTokenProvider` remain available for advanced/manual scenarios. They are no longer the recommended default startup path.

## Build and test

```bash
dotnet build KickNetSdk.slnx
dotnet test KickNetSdk.slnx
```
