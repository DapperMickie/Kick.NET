using System.Net.Http;

namespace Kick;

public sealed class KickOAuthClient : KickServiceClientBase
{
    private readonly KickClientOptions _options;

    public KickOAuthClient(HttpClient httpClient, IKickAccessTokenProvider? tokenProvider = null, KickClientOptions? options = null)
        : base(httpClient, tokenProvider, (options ?? new KickClientOptions()).JsonSerializerOptions, (options ?? new KickClientOptions()).OAuthBaseUri)
    {
        _options = options ?? new KickClientOptions();
    }

    public Uri BuildAuthorizationUri(AuthorizationUrlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new UriBuilder(new Uri(_options.OAuthBaseUri, "oauth/authorize"));
        var query = new KickQueryBuilder()
            .Add("response_type", request.ResponseType)
            .Add("client_id", request.ClientId)
            .Add("redirect", request.Redirect)
            .Add("redirect_uri", request.RedirectUri)
            .Add("scope", request.Scope)
            .Add("code_challenge", request.CodeChallenge)
            .Add("code_challenge_method", request.CodeChallengeMethod)
            .Add("state", request.State);
        builder.Query = query.ToString();
        return builder.Uri;
    }

    public Task<OAuthTokenResponse?> ExchangeAuthorizationCodeAsync(AuthorizationCodeTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostFormAsync<OAuthTokenResponse>(
            "/oauth/token",
            [
                Pair("grant_type", "authorization_code"),
                Pair("client_id", request.ClientId),
                Pair("client_secret", request.ClientSecret),
                Pair("redirect_uri", request.RedirectUri),
                Pair("code_verifier", request.CodeVerifier),
                Pair("code", request.Code),
            ],
            cancellationToken: cancellationToken);
    }

    public Task<OAuthTokenResponse?> CreateAppAccessTokenAsync(AppAccessTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostFormAsync<OAuthTokenResponse>(
            "/oauth/token",
            [
                Pair("grant_type", "client_credentials"),
                Pair("client_id", request.ClientId),
                Pair("client_secret", request.ClientSecret),
            ],
            cancellationToken: cancellationToken);
    }

    public Task<OAuthTokenResponse?> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostFormAsync<OAuthTokenResponse>(
            "/oauth/token",
            [
                Pair("grant_type", "refresh_token"),
                Pair("client_id", request.ClientId),
                Pair("client_secret", request.ClientSecret),
                Pair("refresh_token", request.RefreshToken),
            ],
            cancellationToken: cancellationToken);
    }

    public async Task RevokeTokenAsync(RevokeTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await PostAsync(
            "/oauth/revoke",
            query => query.Add("token", request.Token).Add("token_type_hint", request.TokenTypeHint),
            authenticated: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task<KickResponse<TokenIntrospection>?> IntrospectCurrentTokenAsync(CancellationToken cancellationToken = default)
        => PostJsonAsync<KickResponse<TokenIntrospection>>("/oauth/token/introspect", null, authenticated: true, cancellationToken: cancellationToken);

    private static KeyValuePair<string, string> Pair(string key, string value) => new(key, value);
}
