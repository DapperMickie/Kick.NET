# Sample Projects

The repository now includes several focused samples, each centered on a different SDK use-case.

## Sample catalog

- [samples/KickNet.ConsoleExamples](D:/Projects/Kick.NET/samples/KickNet.ConsoleExamples)
  General command-based reference app that shows the default app-auth startup model and the separate user-auth path.

- [samples/KickNet.BotLoginWebhookSample](D:/Projects/Kick.NET/samples/KickNet.BotLoginWebhookSample)
  Minimal API app showing the exceptional user-login path, OAuth callback handling, event subscription, webhook verification, and pretty console logging.

- [samples/KickNet.RewardOpsSample](D:/Projects/Kick.NET/samples/KickNet.RewardOpsSample)
  Console operator tool for reward lifecycle work: list rewards, create/update rewards, view pending redemptions, and accept or reject them.

- [samples/KickNet.ChatOpsSample](D:/Projects/Kick.NET/samples/KickNet.ChatOpsSample)
  Interactive console REPL for live chat and moderation actions: send messages, reply, delete, ban, and unban.

- [samples/KickNet.ChannelDashboardSample](D:/Projects/Kick.NET/samples/KickNet.ChannelDashboardSample)
  Minimal API dashboard for channel inspection and metadata updates, with livestream discovery views.

## Which sample to start from

- If you need OAuth and webhook handling, start with `KickNet.BotLoginWebhookSample`.
- If you need a broad SDK reference, start with `KickNet.ConsoleExamples`.
- If you are building moderator or broadcaster tooling, start with `KickNet.ChatOpsSample` or `KickNet.RewardOpsSample`.
- If you are building a control panel or admin UI, start with `KickNet.ChannelDashboardSample`.

## Common environment variables

Most samples use one or more of these:

- `KICK_CLIENT_ID`
- `KICK_CLIENT_SECRET`
- `KICK_BROADCASTER_USER_ID`
- `KICK_REWARD_ID`
- `KICK_REDEMPTION_IDS`
- `KICK_USER_CLIENT_ID`
- `KICK_USER_CLIENT_SECRET`
- `Kick__ClientId`
- `Kick__ClientSecret`
- `Kick__RedirectUri`
- `Kick__WebhookPublicUrl`
