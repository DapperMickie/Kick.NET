namespace Kick;

public class CategorySummary
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Thumbnail { get; init; }
}

public sealed class CategoryDetail : CategorySummary
{
    public IReadOnlyList<string>? Tags { get; init; }
    public int? ViewerCount { get; init; }
}

public sealed class CategoryWithTags : CategorySummary
{
    public IReadOnlyList<string>? Tags { get; init; }
}

public sealed class User
{
    public int UserId { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? ProfilePicture { get; init; }
}

public sealed class UserReference
{
    public int UserId { get; init; }
}

public sealed class Channel
{
    public int BroadcasterUserId { get; init; }
    public string? Slug { get; init; }
    public string? StreamTitle { get; init; }
    public string? ChannelDescription { get; init; }
    public string? BannerPicture { get; init; }
    public int? ActiveSubscribersCount { get; init; }
    public int? CanceledSubscribersCount { get; init; }
    public CategorySummary? Category { get; init; }
    public Stream? Stream { get; init; }
}

public sealed class Stream
{
    public bool? IsLive { get; init; }
    public bool? IsMature { get; init; }
    public string? Language { get; init; }
    public string? Url { get; init; }
    public string? Key { get; init; }
    public string? Thumbnail { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public int? ViewerCount { get; init; }
    public IReadOnlyList<string>? CustomTags { get; init; }
}

public sealed class Livestream
{
    public int BroadcasterUserId { get; init; }
    public int ChannelId { get; init; }
    public string? Slug { get; init; }
    public string? StreamTitle { get; init; }
    public string? Language { get; init; }
    public bool? HasMatureContent { get; init; }
    public string? ProfilePicture { get; init; }
    public string? Thumbnail { get; init; }
    public int? ViewerCount { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public IReadOnlyList<string>? CustomTags { get; init; }
    public CategorySummary? Category { get; init; }
}

public sealed class LivestreamStats
{
    public int TotalCount { get; init; }
}

public sealed class ChannelReward
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public int? Cost { get; init; }
    public string? BackgroundColor { get; init; }
    public bool? IsEnabled { get; init; }
    public bool? IsPaused { get; init; }
    public bool? IsUserInputRequired { get; init; }
    public bool? ShouldRedemptionsSkipRequestQueue { get; init; }
}

public sealed class MinimalChannelReward
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public ulong? Cost { get; init; }
    public bool? CanManage { get; init; }
    public bool? IsDeleted { get; init; }
}

public sealed class ChannelRewardRedemption
{
    public string? Id { get; init; }
    public DateTimeOffset? RedeemedAt { get; init; }
    public UserReference? Redeemer { get; init; }
    public string? Status { get; init; }
    public string? UserInput { get; init; }
}

public sealed class RedemptionsByReward
{
    public MinimalChannelReward? Reward { get; init; }
    public IReadOnlyList<ChannelRewardRedemption>? Redemptions { get; init; }
}

public sealed class FailedRedemption
{
    public string? Id { get; init; }
    public string? Reason { get; init; }
}

public sealed class ChatMessageResponse
{
    public bool? IsSent { get; init; }
    public string? MessageId { get; init; }
}

public sealed class EventSubscription
{
    public string? Id { get; init; }
    public string? AppId { get; init; }
    public int? BroadcasterUserId { get; init; }
    public string? Event { get; init; }
    public int? Version { get; init; }
    public string? Method { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
}

public sealed class CreatedEventSubscription
{
    public string? Name { get; init; }
    public int Version { get; init; }
    public string? SubscriptionId { get; init; }
    public string? Error { get; init; }
}

public sealed class KickEventDefinition
{
    public string? Name { get; init; }
    public int Version { get; init; }
}

public sealed class KicksLeaderboard
{
    public IReadOnlyList<KicksLeaderboardEntry>? Lifetime { get; init; }
    public IReadOnlyList<KicksLeaderboardEntry>? Month { get; init; }
    public IReadOnlyList<KicksLeaderboardEntry>? Week { get; init; }
}

public sealed class KicksLeaderboardEntry
{
    public int UserId { get; init; }
    public string? Username { get; init; }
    public int Rank { get; init; }
    public int GiftedAmount { get; init; }
}

public sealed class PublicKeyResponse
{
    public string? PublicKey { get; init; }
}

public sealed class OAuthTokenResponse
{
    public string? AccessToken { get; init; }
    public string? TokenType { get; init; }
    public string? RefreshToken { get; init; }
    public int? ExpiresIn { get; init; }
    public string? Scope { get; init; }
}

public sealed class OAuthErrorResponse
{
    public string? Error { get; init; }
}

public sealed class TokenIntrospection
{
    public bool Active { get; init; }
    public string? ClientId { get; init; }
    public string? TokenType { get; init; }
    public string? Scope { get; init; }
    public long Exp { get; init; }
}

public sealed class DeleteModerationBansResponse
{
}

public sealed class PostModerationBansResponse
{
}
