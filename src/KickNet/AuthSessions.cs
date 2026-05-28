using System.Net.Http;

namespace Kick;

public sealed class KickAppCredentials
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}

public sealed class KickAppSessionOptions
{
    public KickClientOptions? ClientOptions { get; init; }
    public TimeSpan TokenRefreshBuffer { get; init; } = TimeSpan.FromMinutes(1);
}

public sealed class KickUserAuthOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public IReadOnlyList<string>? DefaultScopes { get; init; }
    public KickClientOptions? ClientOptions { get; init; }
}

public sealed class AppTokenInfo
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = string.Empty;
    public string? Scope { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed class UserTokenInfo
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = string.Empty;
    public string? RefreshToken { get; init; }
    public string? Scope { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed class KickUserLoginRequest
{
    public Uri AuthorizationUri { get; init; } = new("https://id.kick.com/");
    public string State { get; init; } = string.Empty;
    public string CodeVerifier { get; init; } = string.Empty;
    public string CodeChallenge { get; init; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; init; } = [];
}

public interface IKickAppTokenProvider : IKickAccessTokenProvider
{
    Task<AppTokenInfo> GetCurrentTokenAsync(CancellationToken cancellationToken = default);
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}

public sealed class KickAppSession
{
    private readonly KickAppTokenManager _tokenManager;

    internal KickAppSession(KickAppCredentials credentials, KickAppSessionOptions? options, HttpClient? httpClient = null)
    {
        Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        Options = options ?? new KickAppSessionOptions();
        ClientOptions = Options.ClientOptions ?? new KickClientOptions();
        HttpClient = httpClient ?? new HttpClient();
        _tokenManager = new KickAppTokenManager(HttpClient, Credentials, ClientOptions, Options.TokenRefreshBuffer);
        Client = new KickClient(HttpClient, _tokenManager, ClientOptions);
    }

    public KickAppCredentials Credentials { get; }
    public KickAppSessionOptions Options { get; }
    public KickClientOptions ClientOptions { get; }
    public HttpClient HttpClient { get; }
    public KickClient Client { get; }

    public Task<AppTokenInfo> GetCurrentTokenAsync(CancellationToken cancellationToken = default)
        => _tokenManager.GetCurrentTokenAsync(cancellationToken);

    public Task InvalidateAsync(CancellationToken cancellationToken = default)
        => _tokenManager.InvalidateAsync(cancellationToken);
}

public sealed class KickUserSession
{
    private readonly KickOAuthClient _oauthClient;
    private readonly UserSessionTokenProvider _tokenProvider;

    internal KickUserSession(KickUserAuthOptions options, KickClientOptions clientOptions, HttpClient httpClient, UserTokenInfo tokenInfo)
    {
        Options = options;
        ClientOptions = clientOptions;
        HttpClient = httpClient;
        _oauthClient = new KickOAuthClient(httpClient, options: clientOptions);
        _tokenProvider = new UserSessionTokenProvider(tokenInfo);
        Client = new KickClient(httpClient, _tokenProvider, clientOptions);
    }

    public KickUserAuthOptions Options { get; }
    public KickClientOptions ClientOptions { get; }
    public HttpClient HttpClient { get; }
    public KickClient Client { get; }

    public UserTokenInfo TokenInfo => _tokenProvider.TokenInfo;

    public async Task<UserTokenInfo> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(TokenInfo.RefreshToken))
        {
            throw new InvalidOperationException("The current user session does not contain a refresh token.");
        }

        var response = await _oauthClient.RefreshTokenAsync(
            new RefreshTokenRequest
            {
                ClientId = Options.ClientId,
                ClientSecret = Options.ClientSecret,
                RefreshToken = TokenInfo.RefreshToken,
            },
            cancellationToken).ConfigureAwait(false);

        var tokenInfo = ToUserTokenInfo(response);
        _tokenProvider.Set(tokenInfo);
        return tokenInfo;
    }

    public async Task RevokeAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(TokenInfo.AccessToken))
        {
            await _oauthClient.RevokeTokenAsync(
                new RevokeTokenRequest
                {
                    Token = TokenInfo.AccessToken,
                    TokenTypeHint = "access_token",
                },
                cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(TokenInfo.RefreshToken))
        {
            await _oauthClient.RevokeTokenAsync(
                new RevokeTokenRequest
                {
                    Token = TokenInfo.RefreshToken,
                    TokenTypeHint = "refresh_token",
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    internal static UserTokenInfo ToUserTokenInfo(OAuthTokenResponse? response)
    {
        if (response?.AccessToken is null)
        {
            throw new InvalidOperationException("KICK did not return an access token.");
        }

        return new UserTokenInfo
        {
            AccessToken = response.AccessToken,
            TokenType = response.TokenType ?? "Bearer",
            RefreshToken = response.RefreshToken,
            Scope = response.Scope,
            ExpiresAtUtc = response.ExpiresIn is null ? null : DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn.Value),
        };
    }
}

public sealed class KickUserAuthClient
{
    private readonly KickOAuthClient _oauthClient;
    private readonly HttpClient _httpClient;
    private readonly KickClientOptions _clientOptions;

    public KickUserAuthClient(KickUserAuthOptions options, HttpClient? httpClient = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _clientOptions = options.ClientOptions ?? new KickClientOptions();
        _httpClient = httpClient ?? new HttpClient();
        _oauthClient = new KickOAuthClient(_httpClient, options: _clientOptions);
    }

    public KickUserAuthOptions Options { get; }

    public KickUserLoginRequest CreateLoginRequest(IEnumerable<string>? scopes = null, string? state = null, string? sacrificialRedirect = null)
    {
        ValidateUserAuthOptions(Options);

        var scopeList = (scopes ?? Options.DefaultScopes ?? []).Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (scopeList.Length == 0)
        {
            throw new InvalidOperationException("At least one scope is required to create a user login request.");
        }

        var codeVerifier = KickPkce.GenerateCodeVerifier();
        var codeChallenge = KickPkce.CreateCodeChallenge(codeVerifier);
        var request = new AuthorizationUrlRequestBuilder()
            .WithClientId(Options.ClientId)
            .WithRedirectUri(Options.RedirectUri)
            .WithState(state ?? Guid.NewGuid().ToString("N"))
            .WithCodeChallenge(codeChallenge);

        foreach (var scope in scopeList)
        {
            request.AddScope(scope);
        }

        if (!string.IsNullOrWhiteSpace(sacrificialRedirect))
        {
            request.WithSacrificialRedirect(sacrificialRedirect);
        }

        var authorizationRequest = request.Build();

        return new KickUserLoginRequest
        {
            AuthorizationUri = _oauthClient.BuildAuthorizationUri(authorizationRequest),
            State = authorizationRequest.State,
            CodeVerifier = codeVerifier,
            CodeChallenge = codeChallenge,
            Scopes = scopeList,
        };
    }

    public async Task<KickUserSession> ExchangeAuthorizationCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        ValidateUserAuthOptions(Options);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);

        var response = await _oauthClient.ExchangeAuthorizationCodeAsync(
            new AuthorizationCodeTokenRequest
            {
                ClientId = Options.ClientId,
                ClientSecret = Options.ClientSecret,
                RedirectUri = Options.RedirectUri,
                CodeVerifier = codeVerifier,
                Code = code,
            },
            cancellationToken).ConfigureAwait(false);

        return new KickUserSession(Options, _clientOptions, _httpClient, KickUserSession.ToUserTokenInfo(response));
    }

    public async Task<KickUserSession> RefreshSessionAsync(KickUserSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var token = await session.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return new KickUserSession(Options, _clientOptions, _httpClient, token);
    }

    public Task RevokeAsync(KickUserSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.RevokeAsync(cancellationToken);
    }

    private static void ValidateUserAuthOptions(KickUserAuthOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret) ||
            string.IsNullOrWhiteSpace(options.RedirectUri))
        {
            throw new InvalidOperationException("Set ClientId, ClientSecret, and RedirectUri before attempting a user login.");
        }
    }
}

public static class KickSdk
{
    public static KickAppSession CreateAppSession(KickAppCredentials credentials, KickClientOptions? options = null, HttpClient? httpClient = null)
    {
        return new KickAppSession(credentials, new KickAppSessionOptions
        {
            ClientOptions = options,
        }, httpClient);
    }

    public static KickUserAuthClient CreateUserAuthClient(KickUserAuthOptions options, KickClientOptions? clientOptions = null, HttpClient? httpClient = null)
    {
        if (clientOptions is not null)
        {
            options = new KickUserAuthOptions
            {
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret,
                RedirectUri = options.RedirectUri,
                DefaultScopes = options.DefaultScopes,
                ClientOptions = clientOptions,
            };
        }

        return new KickUserAuthClient(options, httpClient);
    }
}

public static class KickAppSessionFactory
{
    public static KickAppSession Create(KickAppCredentials credentials, KickClientOptions? options = null, HttpClient? httpClient = null)
        => KickSdk.CreateAppSession(credentials, options, httpClient);
}

internal sealed class KickAppTokenManager : IKickAppTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly KickAppCredentials _credentials;
    private readonly KickClientOptions _clientOptions;
    private readonly TimeSpan _refreshBuffer;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private AppTokenInfo? _tokenInfo;

    public KickAppTokenManager(HttpClient httpClient, KickAppCredentials credentials, KickClientOptions clientOptions, TimeSpan refreshBuffer)
    {
        _httpClient = httpClient;
        _credentials = credentials;
        _clientOptions = clientOptions;
        _refreshBuffer = refreshBuffer;
        ValidateCredentials(_credentials);
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetCurrentTokenAsync(cancellationToken).ConfigureAwait(false);
        return token.AccessToken;
    }

    public async Task<AppTokenInfo> GetCurrentTokenAsync(CancellationToken cancellationToken = default)
    {
        if (IsCurrent(_tokenInfo))
        {
            return _tokenInfo!;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsCurrent(_tokenInfo))
            {
                return _tokenInfo!;
            }

            var oauthClient = new KickOAuthClient(_httpClient, options: _clientOptions);
            var response = await oauthClient.CreateAppAccessTokenAsync(
                new AppAccessTokenRequest
                {
                    ClientId = _credentials.ClientId,
                    ClientSecret = _credentials.ClientSecret,
                },
                cancellationToken).ConfigureAwait(false);

            if (response?.AccessToken is null)
            {
                throw new InvalidOperationException("KICK did not return an app access token.");
            }

            _tokenInfo = new AppTokenInfo
            {
                AccessToken = response.AccessToken,
                TokenType = response.TokenType ?? "Bearer",
                Scope = response.Scope,
                ExpiresAtUtc = response.ExpiresIn is null ? null : DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn.Value),
            };

            return _tokenInfo;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        _tokenInfo = null;
        return Task.CompletedTask;
    }

    private bool IsCurrent(AppTokenInfo? token)
    {
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return false;
        }

        if (token.ExpiresAtUtc is null)
        {
            return true;
        }

        return token.ExpiresAtUtc.Value > DateTimeOffset.UtcNow.Add(_refreshBuffer);
    }

    private static void ValidateCredentials(KickAppCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.ClientId) || string.IsNullOrWhiteSpace(credentials.ClientSecret))
        {
            throw new InvalidOperationException("Set ClientId and ClientSecret before creating an app-authenticated KICK session.");
        }
    }
}

internal sealed class UserSessionTokenProvider(UserTokenInfo tokenInfo) : IKickAccessTokenProvider
{
    public UserTokenInfo TokenInfo { get; private set; } = tokenInfo ?? throw new ArgumentNullException(nameof(tokenInfo));

    public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(TokenInfo.AccessToken);
    }

    public void Set(UserTokenInfo tokenInfo)
    {
        TokenInfo = tokenInfo ?? throw new ArgumentNullException(nameof(tokenInfo));
    }
}
