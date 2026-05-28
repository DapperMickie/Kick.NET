using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kick;

public interface IKickWebhookEvent
{
}

public sealed class KickWebhookHeaders
{
    public string MessageId { get; init; } = string.Empty;
    public string SubscriptionId { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public string MessageTimestamp { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EventVersion { get; init; } = string.Empty;
}

public sealed class KickWebhookEnvelope<T>(KickWebhookHeaders headers, T payload)
{
    public KickWebhookHeaders Headers { get; } = headers;
    public T Payload { get; } = payload;
}

public sealed class KickWebhookVerifier
{
    public const string DefaultPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAq/+l1WnlRrGSolDMA+A8
        6rAhMbQGmQ2SapVcGM3zq8ANXjnhDWocMqfWcTd95btDydITa10kDvHzw9WQOqp2
        MZI7ZyrfzJuz5nhTPCiJwTwnEtWft7nV14BYRDHvlfqPUaZ+1KR4OCaO/wWIk/rQ
        L/TjY0M70gse8rlBkbo2a8rKhu69RQTRsoaf4DVhDPEeSeI5jVrRDGAMGL3cGuyY
        6CLKGdjVEM78g3JfYOvDU/RvfqD7L89TZ3iN94jrmWdGz34JNlEI5hqK8dd7C5EF
        BEbZ5jgB8s8ReQV8H+MkuffjdAj3ajDDX3DOJMIut1lBrUVD1AaSrGCKHooWoL2e
        twIDAQAB
        -----END PUBLIC KEY-----
        """;

    private readonly RSA _rsa;

    public KickWebhookVerifier(string publicKeyPem)
    {
        _rsa = KickCryptography.CreateRsaFromPublicKeyPem(publicKeyPem);
    }

    public KickWebhookVerifier(RSA rsa)
    {
        _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa));
    }

    public bool Verify(KickWebhookHeaders headers, ReadOnlySpan<byte> rawBody)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var payload = Encoding.UTF8.GetBytes($"{headers.MessageId}.{headers.MessageTimestamp}.{Encoding.UTF8.GetString(rawBody)}");
        var hash = SHA256.HashData(payload);
        var signature = Convert.FromBase64String(headers.Signature);
        return _rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}

public static class KickWebhookParser
{
    public static IKickWebhookEvent Parse(string eventType, string json, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var serializerOptions = options ?? KickClientOptions.CreateJsonSerializerOptions();
        return eventType switch
        {
            KickWebhookEventNames.ChatMessageSent => Deserialize<ChatMessageSentEvent>(json, serializerOptions),
            KickWebhookEventNames.ChannelFollowed => Deserialize<ChannelFollowedEvent>(json, serializerOptions),
            KickWebhookEventNames.ChannelSubscriptionRenewal => Deserialize<ChannelSubscriptionRenewalEvent>(json, serializerOptions),
            KickWebhookEventNames.ChannelSubscriptionGifts => Deserialize<ChannelSubscriptionGiftsEvent>(json, serializerOptions),
            KickWebhookEventNames.ChannelSubscriptionNew => Deserialize<ChannelSubscriptionNewEvent>(json, serializerOptions),
            KickWebhookEventNames.ChannelRewardRedemptionUpdated => Deserialize<ChannelRewardRedemptionUpdatedEvent>(json, serializerOptions),
            KickWebhookEventNames.LivestreamStatusUpdated => Deserialize<LivestreamStatusUpdatedEvent>(json, serializerOptions),
            KickWebhookEventNames.LivestreamMetadataUpdated => Deserialize<LivestreamMetadataUpdatedEvent>(json, serializerOptions),
            KickWebhookEventNames.ModerationBanned => Deserialize<ModerationBannedEvent>(json, serializerOptions),
            KickWebhookEventNames.KicksGifted => Deserialize<KicksGiftedEvent>(json, serializerOptions),
            _ => throw new NotSupportedException($"Unsupported KICK webhook event type '{eventType}'."),
        };
    }

    private static T Deserialize<T>(string json, JsonSerializerOptions options) where T : IKickWebhookEvent
        => JsonSerializer.Deserialize<T>(json, options) ?? throw new JsonException($"Unable to deserialize {typeof(T).Name}.");
}

public sealed class KickIdentity
{
    public string? UsernameColor { get; init; }
    public IReadOnlyList<KickBadge>? Badges { get; init; }
}

public sealed class KickBadge
{
    public string? Text { get; init; }
    public string? Type { get; init; }
    public int? Count { get; init; }
}

public class KickWebhookUser
{
    public bool? IsAnonymous { get; init; }
    public int? UserId { get; init; }
    public string? Username { get; init; }
    public bool? IsVerified { get; init; }
    public string? ProfilePicture { get; init; }
    public string? ChannelSlug { get; init; }
    public KickIdentity? Identity { get; init; }
}

public sealed class KickChatReply
{
    public string? MessageId { get; init; }
    public string? Content { get; init; }
    public KickWebhookUser? Sender { get; init; }
}

public sealed class KickEmotePosition
{
    public int S { get; init; }
    public int E { get; init; }
}

public sealed class KickEmote
{
    public string? EmoteId { get; init; }
    public IReadOnlyList<KickEmotePosition>? Positions { get; init; }
}

public sealed class ChatMessageSentEvent : IKickWebhookEvent
{
    public string? MessageId { get; init; }
    public KickChatReply? RepliesTo { get; init; }
    public KickWebhookUser? Broadcaster { get; init; }
    public KickWebhookUser? Sender { get; init; }
    public string? Content { get; init; }
    public IReadOnlyList<KickEmote>? Emotes { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed class ChannelFollowedEvent : IKickWebhookEvent
{
    public KickWebhookUser? Broadcaster { get; init; }
    public KickWebhookUser? Follower { get; init; }
}

public sealed class ChannelSubscriptionRenewalEvent : IKickWebhookEvent
{
    public KickWebhookUser? Broadcaster { get; init; }
    public KickWebhookUser? Subscriber { get; init; }
    public int? Duration { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class ChannelSubscriptionGiftsEvent : IKickWebhookEvent
{
    public KickWebhookUser? Broadcaster { get; init; }
    public KickWebhookUser? Gifter { get; init; }
    public IReadOnlyList<KickWebhookUser>? Giftees { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class ChannelSubscriptionNewEvent : IKickWebhookEvent
{
    public KickWebhookUser? Broadcaster { get; init; }
    public KickWebhookUser? Subscriber { get; init; }
    public int? Duration { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class ChannelRewardRedemptionUpdatedEvent : IKickWebhookEvent
{
    public string? Id { get; init; }
    public string? UserInput { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset? RedeemedAt { get; init; }
    public MinimalChannelReward? Reward { get; init; }
    public KickWebhookUser? Redeemer { get; init; }
    public KickWebhookUser? Broadcaster { get; init; }
}

public sealed class LivestreamStatusUpdatedEvent : IKickWebhookEvent
{
    public KickWebhookUser? Broadcaster { get; init; }
    public bool? IsLive { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
}

public sealed class LivestreamMetadataPayload
{
    public string? Title { get; init; }
    public string? Language { get; init; }
    public bool? HasMatureContent { get; init; }
    public CategorySummary? Category { get; init; }
}

public sealed class LivestreamMetadataUpdatedEvent : IKickWebhookEvent
{
    public KickWebhookUser? Broadcaster { get; init; }
    public LivestreamMetadataPayload? Metadata { get; init; }
}

public sealed class ModerationMetadata
{
    public string? Reason { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class ModerationBannedEvent : IKickWebhookEvent
{
    public KickWebhookUser? Broadcaster { get; init; }
    public KickWebhookUser? Moderator { get; init; }
    public KickWebhookUser? BannedUser { get; init; }
    public ModerationMetadata? Metadata { get; init; }
}

public sealed class KicksGiftInfo
{
    public int? Amount { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Tier { get; init; }
    public string? Message { get; init; }
    public int? PinnedTimeSeconds { get; init; }
}

public sealed class KicksGiftedEvent : IKickWebhookEvent
{
    public KickWebhookUser? Broadcaster { get; init; }
    public KickWebhookUser? Sender { get; init; }
    public KicksGiftInfo? Gift { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}
