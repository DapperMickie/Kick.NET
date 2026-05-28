namespace Kick;

public sealed class GetCategoriesRequest
{
    public string Query { get; init; } = string.Empty;
    public int? Page { get; init; }
}

public sealed class GetCategoriesV2Request
{
    public string? Cursor { get; init; }
    public int? Limit { get; init; }
    public IReadOnlyList<string>? Names { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public IReadOnlyList<int>? Ids { get; init; }
}

public sealed class GetUsersRequest
{
    public IReadOnlyList<int>? Ids { get; init; }
}

public sealed class GetChannelsRequest
{
    public IReadOnlyList<int>? BroadcasterUserIds { get; init; }
    public IReadOnlyList<string>? Slugs { get; init; }
}

public sealed class UpdateChannelRequest
{
    public int? CategoryId { get; init; }
    public string? StreamTitle { get; init; }
    public IReadOnlyList<string>? CustomTags { get; init; }
}

public sealed class UpdateChannelRequestBuilder
{
    private int? _categoryId;
    private string? _streamTitle;
    private readonly List<string> _customTags = [];

    public UpdateChannelRequestBuilder WithCategoryId(int categoryId)
    {
        _categoryId = categoryId;
        return this;
    }

    public UpdateChannelRequestBuilder WithStreamTitle(string streamTitle)
    {
        _streamTitle = streamTitle;
        return this;
    }

    public UpdateChannelRequestBuilder AddCustomTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            _customTags.Add(tag);
        }

        return this;
    }

    public UpdateChannelRequest Build() => new()
    {
        CategoryId = _categoryId,
        StreamTitle = _streamTitle,
        CustomTags = _customTags.Count == 0 ? null : _customTags,
    };
}

public sealed class CreateChannelRewardRequest
{
    public string Title { get; init; } = string.Empty;
    public int Cost { get; init; }
    public string? Description { get; init; }
    public string? BackgroundColor { get; init; }
    public bool? IsEnabled { get; init; }
    public bool? IsUserInputRequired { get; init; }
    public bool? ShouldRedemptionsSkipRequestQueue { get; init; }
}

public sealed class CreateChannelRewardRequestBuilder
{
    private string? _title;
    private int? _cost;
    private string? _description;
    private string? _backgroundColor;
    private bool? _isEnabled;
    private bool? _isUserInputRequired;
    private bool? _skipQueue;

    public CreateChannelRewardRequestBuilder WithTitle(string title) { _title = title; return this; }
    public CreateChannelRewardRequestBuilder WithCost(int cost) { _cost = cost; return this; }
    public CreateChannelRewardRequestBuilder WithDescription(string? description) { _description = description; return this; }
    public CreateChannelRewardRequestBuilder WithBackgroundColor(string? backgroundColor) { _backgroundColor = backgroundColor; return this; }
    public CreateChannelRewardRequestBuilder IsEnabled(bool isEnabled = true) { _isEnabled = isEnabled; return this; }
    public CreateChannelRewardRequestBuilder RequiresUserInput(bool requiresUserInput = true) { _isUserInputRequired = requiresUserInput; return this; }
    public CreateChannelRewardRequestBuilder SkipRequestQueue(bool skipQueue = true) { _skipQueue = skipQueue; return this; }

    public CreateChannelRewardRequest Build() => new()
    {
        Title = _title ?? throw new InvalidOperationException("Title is required."),
        Cost = _cost ?? throw new InvalidOperationException("Cost is required."),
        Description = _description,
        BackgroundColor = _backgroundColor,
        IsEnabled = _isEnabled,
        IsUserInputRequired = _isUserInputRequired,
        ShouldRedemptionsSkipRequestQueue = _skipQueue,
    };
}

public sealed class UpdateChannelRewardRequest
{
    public string? Title { get; init; }
    public int? Cost { get; init; }
    public string? Description { get; init; }
    public string? BackgroundColor { get; init; }
    public bool? IsEnabled { get; init; }
    public bool? IsPaused { get; init; }
    public bool? IsUserInputRequired { get; init; }
    public bool? ShouldRedemptionsSkipRequestQueue { get; init; }
}

public sealed class UpdateChannelRewardRequestBuilder
{
    private string? _title;
    private int? _cost;
    private string? _description;
    private string? _backgroundColor;
    private bool? _isEnabled;
    private bool? _isPaused;
    private bool? _isUserInputRequired;
    private bool? _skipQueue;

    public UpdateChannelRewardRequestBuilder WithTitle(string title) { _title = title; return this; }
    public UpdateChannelRewardRequestBuilder WithCost(int cost) { _cost = cost; return this; }
    public UpdateChannelRewardRequestBuilder WithDescription(string? description) { _description = description; return this; }
    public UpdateChannelRewardRequestBuilder WithBackgroundColor(string? backgroundColor) { _backgroundColor = backgroundColor; return this; }
    public UpdateChannelRewardRequestBuilder IsEnabled(bool isEnabled = true) { _isEnabled = isEnabled; return this; }
    public UpdateChannelRewardRequestBuilder IsPaused(bool isPaused = true) { _isPaused = isPaused; return this; }
    public UpdateChannelRewardRequestBuilder RequiresUserInput(bool requiresUserInput = true) { _isUserInputRequired = requiresUserInput; return this; }
    public UpdateChannelRewardRequestBuilder SkipRequestQueue(bool skipQueue = true) { _skipQueue = skipQueue; return this; }

    public UpdateChannelRewardRequest Build() => new()
    {
        Title = _title,
        Cost = _cost,
        Description = _description,
        BackgroundColor = _backgroundColor,
        IsEnabled = _isEnabled,
        IsPaused = _isPaused,
        IsUserInputRequired = _isUserInputRequired,
        ShouldRedemptionsSkipRequestQueue = _skipQueue,
    };
}

public sealed class GetRewardRedemptionsRequest
{
    public string? RewardId { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<string>? Ids { get; init; }
    public string? Cursor { get; init; }
}

public sealed class BulkRedemptionActionRequest
{
    public IReadOnlyList<string> Ids { get; init; } = [];
}

public sealed class BulkRedemptionActionRequestBuilder
{
    private readonly List<string> _ids = [];

    public BulkRedemptionActionRequestBuilder AddId(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            _ids.Add(id);
        }

        return this;
    }

    public BulkRedemptionActionRequest Build() => new()
    {
        Ids = _ids.Count == 0
            ? throw new InvalidOperationException("At least one redemption id is required.")
            : _ids,
    };
}

public sealed class PostChatMessageRequest
{
    public int? BroadcasterUserId { get; init; }
    public string Content { get; init; } = string.Empty;
    public string Type { get; init; } = "user";
    public string? ReplyToMessageId { get; init; }
}

public sealed class PostChatMessageRequestBuilder
{
    private int? _broadcasterUserId;
    private string? _content;
    private string _type = "user";
    private string? _replyToMessageId;

    public PostChatMessageRequestBuilder ForBroadcaster(int broadcasterUserId) { _broadcasterUserId = broadcasterUserId; return this; }
    public PostChatMessageRequestBuilder WithContent(string content) { _content = content; return this; }
    public PostChatMessageRequestBuilder AsBot() { _type = "bot"; return this; }
    public PostChatMessageRequestBuilder AsUser() { _type = "user"; return this; }
    public PostChatMessageRequestBuilder ReplyTo(string messageId) { _replyToMessageId = messageId; return this; }

    public PostChatMessageRequest Build() => new()
    {
        BroadcasterUserId = _broadcasterUserId,
        Content = _content ?? throw new InvalidOperationException("Content is required."),
        Type = _type,
        ReplyToMessageId = _replyToMessageId,
    };
}

public sealed class GetEventSubscriptionsRequest
{
    public int? BroadcasterUserId { get; init; }
}

public sealed class CreateEventSubscriptionsRequest
{
    public int? BroadcasterUserId { get; init; }
    public string? Method { get; init; }
    public IReadOnlyList<KickEventDefinition> Events { get; init; } = [];
}

public sealed class CreateEventSubscriptionsRequestBuilder
{
    private int? _broadcasterUserId;
    private string _method = "webhook";
    private readonly List<KickEventDefinition> _events = [];

    public CreateEventSubscriptionsRequestBuilder ForBroadcaster(int broadcasterUserId) { _broadcasterUserId = broadcasterUserId; return this; }
    public CreateEventSubscriptionsRequestBuilder UsingWebhook() { _method = "webhook"; return this; }
    public CreateEventSubscriptionsRequestBuilder AddEvent(string eventName, int version = 1)
    {
        _events.Add(new KickEventDefinition { Name = eventName, Version = version });
        return this;
    }

    public CreateEventSubscriptionsRequest Build() => new()
    {
        BroadcasterUserId = _broadcasterUserId,
        Method = _method,
        Events = _events.Count == 0
            ? throw new InvalidOperationException("At least one event is required.")
            : _events,
    };
}

public sealed class DeleteEventSubscriptionsRequest
{
    public IReadOnlyList<string> Ids { get; init; } = [];
}

public sealed class DeleteEventSubscriptionsRequestBuilder
{
    private readonly List<string> _ids = [];

    public DeleteEventSubscriptionsRequestBuilder AddId(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            _ids.Add(id);
        }

        return this;
    }

    public DeleteEventSubscriptionsRequest Build() => new()
    {
        Ids = _ids.Count == 0 ? throw new InvalidOperationException("At least one subscription id is required.") : _ids,
    };
}

public sealed class GetLivestreamsRequest
{
    public IReadOnlyList<int>? BroadcasterUserIds { get; init; }
    public int? CategoryId { get; init; }
    public string? Language { get; init; }
    public int? Limit { get; init; }
    public string? Sort { get; init; }
}

public sealed class GetChannelWebsiteClipsRequest
{
    public string Channel { get; init; } = string.Empty;
    public int? Page { get; init; }
    public int? Limit { get; init; }
    public string? Sort { get; init; }
}

public sealed class GetWebsiteClipsRequest
{
    public int? Page { get; init; }
    public int? Limit { get; init; }
    public string? Sort { get; init; }
}

public sealed class GetCategoryWebsiteClipsRequest
{
    public string Category { get; init; } = string.Empty;
    public int? Page { get; init; }
    public int? Limit { get; init; }
    public string? Sort { get; init; }
}

public sealed class GetKicksLeaderboardRequest
{
    public int? Top { get; init; }
}

public sealed class BanUserRequest
{
    public int BroadcasterUserId { get; init; }
    public int UserId { get; init; }
    public string? Reason { get; init; }
    public int? Duration { get; init; }
}

public sealed class BanUserRequestBuilder
{
    private int? _broadcasterUserId;
    private int? _userId;
    private string? _reason;
    private int? _duration;

    public BanUserRequestBuilder ForBroadcaster(int broadcasterUserId) { _broadcasterUserId = broadcasterUserId; return this; }
    public BanUserRequestBuilder ForUser(int userId) { _userId = userId; return this; }
    public BanUserRequestBuilder WithReason(string? reason) { _reason = reason; return this; }
    public BanUserRequestBuilder TimeoutForMinutes(int durationMinutes) { _duration = durationMinutes; return this; }
    public BanUserRequestBuilder Permanent() { _duration = null; return this; }

    public BanUserRequest Build() => new()
    {
        BroadcasterUserId = _broadcasterUserId ?? throw new InvalidOperationException("Broadcaster user id is required."),
        UserId = _userId ?? throw new InvalidOperationException("User id is required."),
        Reason = _reason,
        Duration = _duration,
    };
}

public sealed class UnbanUserRequest
{
    public int BroadcasterUserId { get; init; }
    public int UserId { get; init; }
}

public sealed class UnbanUserRequestBuilder
{
    private int? _broadcasterUserId;
    private int? _userId;

    public UnbanUserRequestBuilder ForBroadcaster(int broadcasterUserId) { _broadcasterUserId = broadcasterUserId; return this; }
    public UnbanUserRequestBuilder ForUser(int userId) { _userId = userId; return this; }

    public UnbanUserRequest Build() => new()
    {
        BroadcasterUserId = _broadcasterUserId ?? throw new InvalidOperationException("Broadcaster user id is required."),
        UserId = _userId ?? throw new InvalidOperationException("User id is required."),
    };
}

public sealed class AuthorizationUrlRequest
{
    public string ClientId { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public string ResponseType { get; init; } = "code";
    public string State { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string CodeChallenge { get; init; } = string.Empty;
    public string CodeChallengeMethod { get; init; } = "S256";
    public string? Redirect { get; init; }
}

public sealed class AuthorizationUrlRequestBuilder
{
    private readonly List<string> _scopes = [];
    private string? _clientId;
    private string? _redirectUri;
    private string? _state;
    private string? _codeChallenge;
    private string _responseType = "code";
    private string _codeChallengeMethod = "S256";
    private string? _redirect;

    public AuthorizationUrlRequestBuilder WithClientId(string clientId) { _clientId = clientId; return this; }
    public AuthorizationUrlRequestBuilder WithRedirectUri(string redirectUri) { _redirectUri = redirectUri; return this; }
    public AuthorizationUrlRequestBuilder WithState(string state) { _state = state; return this; }
    public AuthorizationUrlRequestBuilder WithCodeChallenge(string codeChallenge) { _codeChallenge = codeChallenge; return this; }
    public AuthorizationUrlRequestBuilder AddScope(string scope) { if (!string.IsNullOrWhiteSpace(scope)) _scopes.Add(scope); return this; }
    public AuthorizationUrlRequestBuilder WithSacrificialRedirect(string redirect) { _redirect = redirect; return this; }

    public AuthorizationUrlRequest Build() => new()
    {
        ClientId = _clientId ?? throw new InvalidOperationException("ClientId is required."),
        RedirectUri = _redirectUri ?? throw new InvalidOperationException("RedirectUri is required."),
        State = _state ?? throw new InvalidOperationException("State is required."),
        CodeChallenge = _codeChallenge ?? throw new InvalidOperationException("CodeChallenge is required."),
        Scope = _scopes.Count == 0 ? throw new InvalidOperationException("At least one scope is required.") : string.Join(' ', _scopes),
        ResponseType = _responseType,
        CodeChallengeMethod = _codeChallengeMethod,
        Redirect = _redirect,
    };
}

public sealed class AuthorizationCodeTokenRequest
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public string CodeVerifier { get; init; } = string.Empty;
}

public sealed class AppAccessTokenRequest
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}

public sealed class RefreshTokenRequest
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed class RevokeTokenRequest
{
    public string Token { get; init; } = string.Empty;
    public string? TokenTypeHint { get; init; }
}
