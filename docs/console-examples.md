# Console Examples

The repository includes a runnable reference app at [samples/KickNet.ConsoleExamples](D:/Projects/Kick.NET/samples/KickNet.ConsoleExamples).

## Run the help screen

```bash
dotnet run --project samples/KickNet.ConsoleExamples -- help
```

## Commands

- `app-auth`: default client-credentials startup
- `user-auth`: browser login flow and callback exchange example
- `public-read`: categories, users, channels, and leaderboard examples
- `channels`: channel lookup and metadata update examples
- `rewards`: reward CRUD and redemption workflow examples
- `chat`: chat request examples with notes about user-scope requirements
- `events`: event subscription examples
- `livestreams`: livestream list and stats examples
- `moderation`: moderation request examples with notes about user-scope requirements
- `public-key`: fetch the KICK webhook verification public key
- `webhooks`: local signature verification and event parsing example
- `di`: dependency injection registration example
- `all`: runs the example set in sequence

## Default app-auth environment

```bash
KICK_CLIENT_ID=...
KICK_CLIENT_SECRET=...
KICK_BROADCASTER_USER_ID=123456
KICK_CATEGORY_QUERY=rust
KICK_CHANNEL_SLUG=xqc
KICK_CHANNEL_REWARD_ID=01...
KICK_REDEMPTION_IDS=01...,01...
KICK_SUBSCRIPTION_ID=01...
KICK_TARGET_USER_ID=7890
```

## User-auth environment

```bash
KICK_USER_CLIENT_ID=...
KICK_USER_CLIENT_SECRET=...
KICK_REDIRECT_URI=https://localhost/callback
```

## Typical flow

1. Run `app-auth` to confirm your client credentials are the default startup path.
2. Export `KICK_CLIENT_ID` and `KICK_CLIENT_SECRET`.
3. Run `public-read` to validate connectivity.
4. Run targeted flows like `rewards` or `channels`.
5. Use `user-auth` only when a scenario truly needs a user session.
