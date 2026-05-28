# Webhook Guide

The SDK includes:

- `KickWebhookVerifier` for signature validation
- `KickWebhookParser` for typed event deserialization
- `KickWebhookHeaders` for the KICK webhook header set

## Validate a webhook signature

```csharp
using System.Text;
using Kick;

var headers = new KickWebhookHeaders
{
    MessageId = request.Headers["Kick-Event-Message-Id"]!,
    SubscriptionId = request.Headers["Kick-Event-Subscription-Id"]!,
    Signature = request.Headers["Kick-Event-Signature"]!,
    MessageTimestamp = request.Headers["Kick-Event-Message-Timestamp"]!,
    EventType = request.Headers["Kick-Event-Type"]!,
    EventVersion = request.Headers["Kick-Event-Version"]!,
};

var body = await new StreamReader(request.Body).ReadToEndAsync();
var verifier = new KickWebhookVerifier(KickWebhookVerifier.DefaultPublicKeyPem);

if (!verifier.Verify(headers, Encoding.UTF8.GetBytes(body)))
{
    throw new InvalidOperationException("Invalid webhook signature.");
}
```

## Parse the webhook body

```csharp
var webhookEvent = KickWebhookParser.Parse(headers.EventType, body);
```

Supported event names are exposed as constants on `KickWebhookEventNames`.

## Handle strongly typed events

```csharp
var webhookEvent = KickWebhookParser.Parse(headers.EventType, body);

switch (webhookEvent)
{
    case ChatMessageSentEvent message:
        Console.WriteLine(message.Content);
        break;
    case ChannelFollowedEvent follow:
        Console.WriteLine(follow.Follower?.Username);
        break;
    case LivestreamStatusUpdatedEvent stream:
        Console.WriteLine(stream.IsLive);
        break;
}
```

## Fetch the public key from KICK

```csharp
var session = KickSdk.CreateAppSession(new KickAppCredentials
{
    ClientId = "<client-id>",
    ClientSecret = "<client-secret>",
});

var publicKey = await session.Client.PublicKey.GetAsync();
```
