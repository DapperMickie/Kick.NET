using System.Security.Cryptography;
using System.Text;

namespace Kick;

public interface IKickAccessTokenProvider
{
    ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class StaticAccessTokenProvider(string accessToken) : IKickAccessTokenProvider
{
    private readonly string _accessToken = string.IsNullOrWhiteSpace(accessToken)
        ? throw new ArgumentException("Access token cannot be null or empty.", nameof(accessToken))
        : accessToken;

    public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_accessToken);
}

public sealed class DelegateAccessTokenProvider(Func<CancellationToken, ValueTask<string>> factory) : IKickAccessTokenProvider
{
    private readonly Func<CancellationToken, ValueTask<string>> _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) => _factory(cancellationToken);
}

public static class KickScopes
{
    public const string ChannelRead = "channel:read";
    public const string ChannelWrite = "channel:write";
    public const string ChannelRewardsRead = "channel:rewards:read";
    public const string ChannelRewardsWrite = "channel:rewards:write";
    public const string ChatWrite = "chat:write";
    public const string EventsSubscribe = "events:subscribe";
    public const string KicksRead = "kicks:read";
    public const string ModerationBan = "moderation:ban";
    public const string ModerationChatMessageManage = "moderation:chat_message:manage";
    public const string StreamKeyRead = "streamkey:read";
    public const string UserRead = "user:read";
}

public static class KickWebhookEventNames
{
    public const string ChatMessageSent = "chat.message.sent";
    public const string ChannelFollowed = "channel.followed";
    public const string ChannelSubscriptionRenewal = "channel.subscription.renewal";
    public const string ChannelSubscriptionGifts = "channel.subscription.gifts";
    public const string ChannelSubscriptionNew = "channel.subscription.new";
    public const string ChannelRewardRedemptionUpdated = "channel.reward.redemption.updated";
    public const string LivestreamStatusUpdated = "livestream.status.updated";
    public const string LivestreamMetadataUpdated = "livestream.metadata.updated";
    public const string ModerationBanned = "moderation.banned";
    public const string KicksGifted = "kicks.gifted";
}

public static class KickPkce
{
    public static string GenerateCodeVerifier(int byteLength = 32)
    {
        Span<byte> bytes = stackalloc byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public static string CreateCodeChallenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
