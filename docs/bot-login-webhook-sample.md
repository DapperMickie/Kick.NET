# Bot Login + Webhook Sample

The sample app at [samples/KickNet.BotLoginWebhookSample](D:/Projects/Kick.NET/samples/KickNet.BotLoginWebhookSample) demonstrates the exceptional user-auth path:

1. Redirect the browser to KICK OAuth
2. Exchange the callback code through `KickUserAuthClient`
3. Subscribe that logged-in bot account to `chat.message.sent`
4. Receive webhook callbacks at `/webhooks/kick`
5. Verify the KICK signature and pretty-print chat messages to the server console

The normal SDK startup path is app auth with `client_id` and `client_secret`. This sample exists specifically for workflows that need a user session.

## Run it

```bash
dotnet run --project samples/KickNet.BotLoginWebhookSample
```

Then open the local URL shown by ASP.NET Core, usually:

```text
https://localhost:7238
```

## Configuration

Set these values in `appsettings.Development.json` or with environment variables:

```json
{
  "Kick": {
    "ClientId": "your-kick-client-id",
    "ClientSecret": "your-kick-client-secret",
    "RedirectUri": "https://localhost:7238/oauth/callback",
    "WebhookPublicUrl": "https://your-public-tunnel.example"
  }
}
```

Environment variable equivalents:

- `Kick__ClientId`
- `Kick__ClientSecret`
- `Kick__RedirectUri`
- `Kick__WebhookPublicUrl`
- `Kick__WebhookPublicKeyPem`

## KICK app setup

In your KICK developer app settings:

1. Set the redirect URI to exactly match `Kick:RedirectUri`
2. Set the webhook URL to `https://your-public-tunnel.example/webhooks/kick`

The event-subscription API does not include a callback URL in the request payload. KICK uses the webhook URL configured on the app itself, so the app settings need to be correct before you log in.

## Local development

Because KICK has to call your webhook endpoint, localhost alone is not enough. Use a public tunnel such as:

- ngrok
- Cloudflare Tunnel
- a deployed dev environment

Point `Kick:WebhookPublicUrl` and the KICK app webhook URL at that public address.

## Flow

- `GET /` shows setup and session status
- `GET /login` starts the bot login flow with PKCE
- `GET /oauth/callback` exchanges the code into a `KickUserSession`, stores it in memory, and subscribes to `chat.message.sent`
- `POST /webhooks/kick` verifies and logs incoming webhook events
- `POST /logout` revokes local tokens and clears in-memory session state

## Console output

When a chat event arrives, the sample writes a formatted panel to the server console containing:

- message ID
- timestamp
- broadcaster
- sender
- content

That gives you a minimal working bot-login and event-consumption reference without needing any MVC controllers or external storage.
