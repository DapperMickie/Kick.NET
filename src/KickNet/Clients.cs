using System.Net.Http;

namespace Kick;

public sealed class KickClient
{
    private readonly HttpClient _httpClient;
    private readonly KickClientOptions _options;

    public KickClient(HttpClient? httpClient = null, IKickAccessTokenProvider? accessTokenProvider = null, KickClientOptions? options = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _options = options ?? new KickClientOptions();

        Categories = new KickCategoriesClient(_httpClient, accessTokenProvider, _options);
        Users = new KickUsersClient(_httpClient, accessTokenProvider, _options);
        Channels = new KickChannelsClient(_httpClient, accessTokenProvider, _options);
        ChannelRewards = new KickChannelRewardsClient(_httpClient, accessTokenProvider, _options);
        Chat = new KickChatClient(_httpClient, accessTokenProvider, _options);
        Events = new KickEventsClient(_httpClient, accessTokenProvider, _options);
        Kicks = new KickKicksClient(_httpClient, accessTokenProvider, _options);
        Livestreams = new KickLivestreamsClient(_httpClient, accessTokenProvider, _options);
        Moderation = new KickModerationClient(_httpClient, accessTokenProvider, _options);
        PublicKey = new KickPublicKeyClient(_httpClient, accessTokenProvider, _options);
        OAuth = new KickOAuthClient(_httpClient, options: _options);
    }

    public KickCategoriesClient Categories { get; }
    public KickUsersClient Users { get; }
    public KickChannelsClient Channels { get; }
    public KickChannelRewardsClient ChannelRewards { get; }
    public KickChatClient Chat { get; }
    public KickEventsClient Events { get; }
    public KickKicksClient Kicks { get; }
    public KickLivestreamsClient Livestreams { get; }
    public KickModerationClient Moderation { get; }
    public KickPublicKeyClient PublicKey { get; }
    public KickOAuthClient OAuth { get; }
}

public sealed class KickCategoriesClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public Task<KickResponse<IReadOnlyList<CategorySummary>>?> GetAsync(GetCategoriesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetAsync<KickResponse<IReadOnlyList<CategorySummary>>>(
            "/public/v1/categories",
            query => query.Add("q", request.Query).Add("page", request.Page),
            cancellationToken: cancellationToken);
    }

    public Task<KickResponse<CategoryDetail>?> GetByIdAsync(int categoryId, CancellationToken cancellationToken = default)
        => GetAsync<KickResponse<CategoryDetail>>($"/public/v1/categories/{categoryId}", cancellationToken: cancellationToken);

    public Task<KickPaginatedResponse<CategoryWithTags>?> GetV2Async(GetCategoriesV2Request? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new GetCategoriesV2Request();
        return GetAsync<KickPaginatedResponse<CategoryWithTags>>(
            "/public/v2/categories",
            query => query
                .Add("cursor", request.Cursor)
                .Add("limit", request.Limit)
                .Add("name", request.Names)
                .Add("tag", request.Tags)
                .Add("id", request.Ids),
            cancellationToken: cancellationToken);
    }
}

public sealed class KickUsersClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public Task<KickResponse<IReadOnlyList<User>>?> GetAsync(GetUsersRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new GetUsersRequest();
        return GetAsync<KickResponse<IReadOnlyList<User>>>(
            "/public/v1/users",
            query => query.Add("id", request.Ids),
            cancellationToken: cancellationToken);
    }

    public Task<KickResponse<TokenIntrospection>?> IntrospectAsync(CancellationToken cancellationToken = default)
        => PostJsonAsync<KickResponse<TokenIntrospection>>("/public/v1/token/introspect", null, cancellationToken: cancellationToken);
}

public sealed class KickChannelsClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public Task<KickResponse<IReadOnlyList<Channel>>?> GetAsync(GetChannelsRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new GetChannelsRequest();
        return GetAsync<KickResponse<IReadOnlyList<Channel>>>(
            "/public/v1/channels",
            query => query.Add("broadcaster_user_id", request.BroadcasterUserIds).Add("slug", request.Slugs),
            cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(UpdateChannelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync("/public/v1/channels", HttpMethod.Patch, request, cancellationToken: cancellationToken);
    }
}

public sealed class KickChannelRewardsClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public Task<KickResponse<IReadOnlyList<ChannelReward>>?> GetAsync(CancellationToken cancellationToken = default)
        => GetAsync<KickResponse<IReadOnlyList<ChannelReward>>>("/public/v1/channels/rewards", cancellationToken: cancellationToken);

    public Task<KickResponse<ChannelReward>?> CreateAsync(CreateChannelRewardRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostJsonAsync<KickResponse<ChannelReward>>("/public/v1/channels/rewards", request, cancellationToken: cancellationToken);
    }

    public Task<KickResponse<ChannelReward>?> UpdateAsync(string rewardId, UpdateChannelRewardRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rewardId);
        ArgumentNullException.ThrowIfNull(request);
        return PatchJsonAsync<KickResponse<ChannelReward>>($"/public/v1/channels/rewards/{rewardId}", request, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(string rewardId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rewardId);
        return DeleteAsync($"/public/v1/channels/rewards/{rewardId}", cancellationToken: cancellationToken);
    }

    public Task<KickPaginatedResponse<RedemptionsByReward>?> GetRedemptionsAsync(GetRewardRedemptionsRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new GetRewardRedemptionsRequest();
        return GetAsync<KickPaginatedResponse<RedemptionsByReward>>(
            "/public/v1/channels/rewards/redemptions",
            query => query.Add("reward_id", request.RewardId).Add("status", request.Status).Add("id", request.Ids).Add("cursor", request.Cursor),
            cancellationToken: cancellationToken);
    }

    public Task<KickResponse<IReadOnlyList<FailedRedemption>>?> AcceptRedemptionsAsync(BulkRedemptionActionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostJsonAsync<KickResponse<IReadOnlyList<FailedRedemption>>>("/public/v1/channels/rewards/redemptions/accept", request, cancellationToken: cancellationToken);
    }

    public Task<KickResponse<IReadOnlyList<FailedRedemption>>?> RejectRedemptionsAsync(BulkRedemptionActionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostJsonAsync<KickResponse<IReadOnlyList<FailedRedemption>>>("/public/v1/channels/rewards/redemptions/reject", request, cancellationToken: cancellationToken);
    }
}

public sealed class KickChatClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public Task<KickResponse<ChatMessageResponse>?> PostMessageAsync(PostChatMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostJsonAsync<KickResponse<ChatMessageResponse>>("/public/v1/chat", request, cancellationToken: cancellationToken);
    }

    public Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return DeleteAsync($"/public/v1/chat/{messageId}", cancellationToken: cancellationToken);
    }
}

public sealed class KickEventsClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public Task<KickResponse<IReadOnlyList<EventSubscription>>?> GetSubscriptionsAsync(GetEventSubscriptionsRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new GetEventSubscriptionsRequest();
        return GetAsync<KickResponse<IReadOnlyList<EventSubscription>>>(
            "/public/v1/events/subscriptions",
            query => query.Add("broadcaster_user_id", request.BroadcasterUserId),
            cancellationToken: cancellationToken);
    }

    public Task<KickResponse<IReadOnlyList<CreatedEventSubscription>>?> CreateSubscriptionsAsync(CreateEventSubscriptionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostJsonAsync<KickResponse<IReadOnlyList<CreatedEventSubscription>>>("/public/v1/events/subscriptions", request, cancellationToken: cancellationToken);
    }

    public Task DeleteSubscriptionsAsync(DeleteEventSubscriptionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DeleteAsync("/public/v1/events/subscriptions", query => query.Add("id", request.Ids), cancellationToken: cancellationToken);
    }
}

public sealed class KickKicksClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public Task<KickResponse<KicksLeaderboard>?> GetLeaderboardAsync(GetKicksLeaderboardRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new GetKicksLeaderboardRequest();
        return GetAsync<KickResponse<KicksLeaderboard>>(
            "/public/v1/kicks/leaderboard",
            query => query.Add("top", request.Top),
            cancellationToken: cancellationToken);
    }
}

public sealed class KickLivestreamsClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public Task<KickResponse<IReadOnlyList<Livestream>>?> GetAsync(GetLivestreamsRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new GetLivestreamsRequest();
        return GetAsync<KickResponse<IReadOnlyList<Livestream>>>(
            "/public/v1/livestreams",
            query => query
                .Add("broadcaster_user_id", request.BroadcasterUserIds)
                .Add("category_id", request.CategoryId)
                .Add("language", request.Language)
                .Add("limit", request.Limit)
                .Add("sort", request.Sort),
            cancellationToken: cancellationToken);
    }

    public Task<KickResponse<LivestreamStats>?> GetStatsAsync(CancellationToken cancellationToken = default)
        => GetAsync<KickResponse<LivestreamStats>>("/public/v1/livestreams/stats", cancellationToken: cancellationToken);
}

public sealed class KickModerationClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public Task<KickResponse<PostModerationBansResponse>?> BanAsync(BanUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostJsonAsync<KickResponse<PostModerationBansResponse>>("/public/v1/moderation/bans", request, cancellationToken: cancellationToken);
    }

    public Task<KickResponse<DeleteModerationBansResponse>?> UnbanAsync(UnbanUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DeleteAsync<KickResponse<DeleteModerationBansResponse>>("/public/v1/moderation/bans", body: request, cancellationToken: cancellationToken);
    }
}

public sealed class KickPublicKeyClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider, KickClientOptions options)
    : KickServiceClientBase(httpClient, tokenProvider, options.JsonSerializerOptions, options.ApiBaseUri)
{
    public async Task<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<KickResponse<PublicKeyResponse>>("/public/v1/public-key", cancellationToken: cancellationToken).ConfigureAwait(false);
        return response?.Data?.PublicKey;
    }
}
