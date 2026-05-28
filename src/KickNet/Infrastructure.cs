using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kick;

public sealed class KickClientOptions
{
    public Uri ApiBaseUri { get; set; } = new("https://api.kick.com/");
    public Uri OAuthBaseUri { get; set; } = new("https://id.kick.com/");
    public JsonSerializerOptions JsonSerializerOptions { get; } = CreateJsonSerializerOptions();

    internal static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}

public sealed class KickApiException : Exception
{
    public KickApiException(HttpStatusCode statusCode, string? message, string? responseBody)
        : base(message ?? $"The KICK API returned {(int)statusCode}.")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string? ResponseBody { get; }
}

public class KickResponse
{
    public string? Message { get; init; }
}

public class KickResponse<T> : KickResponse
{
    public T? Data { get; init; }
}

public sealed class KickPaginatedResponse<T> : KickResponse<IReadOnlyList<T>>
{
    public KickPagination? Pagination { get; init; }
}

public sealed class KickPagination
{
    public string? NextCursor { get; init; }
}

public sealed class KickErrorResponse : KickResponse<object?>
{
}

public abstract class KickServiceClientBase
{
    private readonly HttpClient _httpClient;
    private readonly IKickAccessTokenProvider? _accessTokenProvider;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly Uri _baseUri;

    protected KickServiceClientBase(HttpClient httpClient, IKickAccessTokenProvider? accessTokenProvider, JsonSerializerOptions serializerOptions, Uri baseUri)
    {
        _httpClient = httpClient;
        _accessTokenProvider = accessTokenProvider;
        _serializerOptions = serializerOptions;
        _baseUri = baseUri;
    }

    protected JsonSerializerOptions SerializerOptions => _serializerOptions;

    protected async Task<T?> GetAsync<T>(string path, Action<KickQueryBuilder>? buildQuery = null, bool authenticated = true, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(path, buildQuery));
        await ApplyAuthorizationAsync(request, authenticated, cancellationToken).ConfigureAwait(false);
        return await SendAsync<T>(request, cancellationToken).ConfigureAwait(false);
    }

    protected async Task<T?> PostJsonAsync<T>(string path, object? body, bool authenticated = true, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(path));
        await ApplyAuthorizationAsync(request, authenticated, cancellationToken).ConfigureAwait(false);
        request.Content = CreateJsonContent(body);
        return await SendAsync<T>(request, cancellationToken).ConfigureAwait(false);
    }

    protected async Task<T?> PostAsync<T>(string path, Action<KickQueryBuilder>? buildQuery = null, object? body = null, bool authenticated = true, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(path, buildQuery));
        await ApplyAuthorizationAsync(request, authenticated, cancellationToken).ConfigureAwait(false);
        if (body is not null)
        {
            request.Content = CreateJsonContent(body);
        }

        return await SendAsync<T>(request, cancellationToken).ConfigureAwait(false);
    }

    protected async Task PostAsync(string path, Action<KickQueryBuilder>? buildQuery = null, object? body = null, bool authenticated = true, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(path, buildQuery));
        await ApplyAuthorizationAsync(request, authenticated, cancellationToken).ConfigureAwait(false);
        if (body is not null)
        {
            request.Content = CreateJsonContent(body);
        }

        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    protected async Task<T?> PatchJsonAsync<T>(string path, object? body, bool authenticated = true, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, BuildUri(path));
        await ApplyAuthorizationAsync(request, authenticated, cancellationToken).ConfigureAwait(false);
        request.Content = CreateJsonContent(body);
        return await SendAsync<T>(request, cancellationToken).ConfigureAwait(false);
    }

    protected async Task<T?> PostFormAsync<T>(string path, IEnumerable<KeyValuePair<string, string>> formValues, bool authenticated = false, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(path));
        await ApplyAuthorizationAsync(request, authenticated, cancellationToken).ConfigureAwait(false);
        request.Content = new FormUrlEncodedContent(formValues);
        return await SendAsync<T>(request, cancellationToken).ConfigureAwait(false);
    }

    protected async Task<T?> DeleteAsync<T>(string path, Action<KickQueryBuilder>? buildQuery = null, object? body = null, bool authenticated = true, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildUri(path, buildQuery));
        await ApplyAuthorizationAsync(request, authenticated, cancellationToken).ConfigureAwait(false);
        if (body is not null)
        {
            request.Content = CreateJsonContent(body);
        }

        return await SendAsync<T>(request, cancellationToken).ConfigureAwait(false);
    }

    protected async Task DeleteAsync(string path, Action<KickQueryBuilder>? buildQuery = null, object? body = null, bool authenticated = true, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildUri(path, buildQuery));
        await ApplyAuthorizationAsync(request, authenticated, cancellationToken).ConfigureAwait(false);
        if (body is not null)
        {
            request.Content = CreateJsonContent(body);
        }

        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    protected async Task SendAsync(string path, HttpMethod method, object? body = null, bool authenticated = true, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, BuildUri(path));
        await ApplyAuthorizationAsync(request, authenticated, cancellationToken).ConfigureAwait(false);
        if (body is not null)
        {
            request.Content = CreateJsonContent(body);
        }

        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private HttpContent CreateJsonContent(object? body)
    {
        return new StringContent(JsonSerializer.Serialize(body, _serializerOptions), Encoding.UTF8, "application/json");
    }

    private Uri BuildUri(string path, Action<KickQueryBuilder>? buildQuery = null)
    {
        var uri = new Uri(_baseUri, path.TrimStart('/'));
        if (buildQuery is null)
        {
            return uri;
        }

        var builder = new UriBuilder(uri);
        var query = new KickQueryBuilder();
        buildQuery(query);
        builder.Query = query.ToString();
        return builder.Uri;
    }

    private async Task ApplyAuthorizationAsync(HttpRequestMessage request, bool authenticated, CancellationToken cancellationToken)
    {
        if (!authenticated)
        {
            return;
        }

        if (_accessTokenProvider is null)
        {
            throw new InvalidOperationException("This operation requires an access token provider.");
        }

        var token = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var content = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new KickApiException(response.StatusCode, TryGetErrorMessage(content), content);
        }

        if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(content, _serializerOptions);
    }

    private async Task SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var content = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new KickApiException(response.StatusCode, TryGetErrorMessage(content), content);
        }
    }

    private static string? TryGetErrorMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            var error = JsonSerializer.Deserialize<KickErrorResponse>(content, KickClientOptions.CreateJsonSerializerOptions());
            return error?.Message;
        }
        catch (JsonException)
        {
            return content;
        }
    }
}

public sealed class KickQueryBuilder
{
    private readonly List<KeyValuePair<string, string>> _pairs = [];

    public KickQueryBuilder Add(string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _pairs.Add(new KeyValuePair<string, string>(name, value));
        }

        return this;
    }

    public KickQueryBuilder Add(string name, int? value)
    {
        if (value.HasValue)
        {
            _pairs.Add(new KeyValuePair<string, string>(name, value.Value.ToString()));
        }

        return this;
    }

    public KickQueryBuilder Add(string name, IEnumerable<string>? values)
    {
        if (values is null)
        {
            return this;
        }

        foreach (var value in values.Where(static x => !string.IsNullOrWhiteSpace(x)))
        {
            _pairs.Add(new KeyValuePair<string, string>(name, value));
        }

        return this;
    }

    public KickQueryBuilder Add(string name, IEnumerable<int>? values)
    {
        if (values is null)
        {
            return this;
        }

        foreach (var value in values)
        {
            _pairs.Add(new KeyValuePair<string, string>(name, value.ToString()));
        }

        return this;
    }

    public override string ToString()
    {
        return string.Join("&", _pairs.Select(static pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }
}

public static class KickCryptography
{
    public static RSA CreateRsaFromPublicKeyPem(string pem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pem);
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }
}
