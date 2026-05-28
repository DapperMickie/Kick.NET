using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Kick;
using Kick.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace KickNet.Tests;

public sealed class KickSdkTests
{
    [Fact]
    public void CreateChannelRewardBuilder_BuildsExpectedRequest()
    {
        var request = new CreateChannelRewardRequestBuilder()
            .WithTitle("Song Request")
            .WithCost(100)
            .WithDescription("Paste a YouTube URL.")
            .WithBackgroundColor("#00e701")
            .IsEnabled()
            .RequiresUserInput()
            .SkipRequestQueue()
            .Build();

        Assert.Equal("Song Request", request.Title);
        Assert.Equal(100, request.Cost);
        Assert.Equal("Paste a YouTube URL.", request.Description);
        Assert.Equal("#00e701", request.BackgroundColor);
        Assert.True(request.IsEnabled);
        Assert.True(request.IsUserInputRequired);
        Assert.True(request.ShouldRedemptionsSkipRequestQueue);
    }

    [Fact]
    public async Task AppSession_AcquiresAndCachesAppToken()
    {
        var handler = new SequenceHandler(
            """
            {"access_token":"app-token-1","token_type":"Bearer","expires_in":3600}
            """,
            """
            {"data":{"is_sent":true,"message_id":"abc-123"},"message":"OK"}
            """,
            """
            {"data":{"is_sent":true,"message_id":"abc-456"},"message":"OK"}
            """);

        using var httpClient = new HttpClient(handler);
        var session = KickSdk.CreateAppSession(
            new KickAppCredentials
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
            },
            httpClient: httpClient);

        var token = await session.GetCurrentTokenAsync();
        Assert.Equal("app-token-1", token.AccessToken);

        await session.Client.Chat.PostMessageAsync(new PostChatMessageRequestBuilder().ForBroadcaster(42).WithContent("one").AsBot().Build());
        await session.Client.Chat.PostMessageAsync(new PostChatMessageRequestBuilder().ForBroadcaster(42).WithContent("two").AsBot().Build());

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("https://id.kick.com/oauth/token", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("grant_type=client_credentials&client_id=client-id&client_secret=client-secret", handler.Bodies[0]);
        Assert.Equal("app-token-1", handler.Requests[1].Headers.Authorization!.Parameter);
        Assert.Equal("app-token-1", handler.Requests[2].Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task AppSession_Invalidate_ReacquiresToken()
    {
        var handler = new SequenceHandler(
            """
            {"access_token":"app-token-1","token_type":"Bearer","expires_in":3600}
            """,
            """
            {"access_token":"app-token-2","token_type":"Bearer","expires_in":3600}
            """);

        using var httpClient = new HttpClient(handler);
        var session = KickSdk.CreateAppSession(
            new KickAppCredentials
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
            },
            httpClient: httpClient);

        var token1 = await session.GetCurrentTokenAsync();
        await session.InvalidateAsync();
        var token2 = await session.GetCurrentTokenAsync();

        Assert.Equal("app-token-1", token1.AccessToken);
        Assert.Equal("app-token-2", token2.AccessToken);
    }

    [Fact]
    public void UserAuthClient_CreatesLoginRequest()
    {
        var auth = KickSdk.CreateUserAuthClient(new KickUserAuthOptions
        {
            ClientId = "client-123",
            ClientSecret = "secret-123",
            RedirectUri = "https://app.example/callback",
            DefaultScopes = [KickScopes.UserRead, KickScopes.ChannelRead],
        });

        var login = auth.CreateLoginRequest(sacrificialRedirect: "127.0.0.1");
        var query = login.AuthorizationUri.Query;

        Assert.StartsWith("https://id.kick.com/oauth/authorize", login.AuthorizationUri.ToString(), StringComparison.Ordinal);
        Assert.Contains("client_id=client-123", query, StringComparison.Ordinal);
        Assert.Contains("redirect=127.0.0.1", query, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=https%3A%2F%2Fapp.example%2Fcallback", query, StringComparison.Ordinal);
        Assert.Contains("scope=user%3Aread%20channel%3Aread", query, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(login.CodeVerifier));
    }

    [Fact]
    public async Task UserAuthClient_Exchange_CreatesUserSession()
    {
        var handler = new SequenceHandler(
            """
            {"access_token":"user-token","token_type":"Bearer","refresh_token":"refresh-token","expires_in":3600,"scope":"user:read"}
            """);

        using var httpClient = new HttpClient(handler);
        var auth = KickSdk.CreateUserAuthClient(
            new KickUserAuthOptions
            {
                ClientId = "client-id",
                ClientSecret = "secret-id",
                RedirectUri = "https://app.example/callback",
            },
            httpClient: httpClient);

        var session = await auth.ExchangeAuthorizationCodeAsync("code-123", "verifier-123");

        Assert.Equal("user-token", session.TokenInfo.AccessToken);
        Assert.Equal("refresh-token", session.TokenInfo.RefreshToken);
        Assert.Equal("https://id.kick.com/oauth/token", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("grant_type=authorization_code", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("code_verifier=verifier-123", handler.Bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public void DependencyInjection_RegistersAppSessionAndUserAuth()
    {
        var services = new ServiceCollection();
        services.AddKickAppSession(new KickAppCredentials
        {
            ClientId = "client-id",
            ClientSecret = "secret-id",
        });
        services.AddKickUserAuth(new KickUserAuthOptions
        {
            ClientId = "client-id",
            ClientSecret = "secret-id",
            RedirectUri = "https://localhost/callback",
        });

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<KickAppSession>());
        Assert.NotNull(provider.GetRequiredService<KickClient>());
        Assert.NotNull(provider.GetRequiredService<KickUserAuthClient>());
    }

    [Fact]
    public async Task LegacyTokenProviderConstructor_StillWorks()
    {
        var handler = new SequenceHandler(
            """
            {"data":{"is_sent":true,"message_id":"abc-123"},"message":"OK"}
            """);

        using var httpClient = new HttpClient(handler);
        var client = new KickChatClient(httpClient, new StaticAccessTokenProvider("token-123"), new KickClientOptions());

        var response = await client.PostMessageAsync(
            new PostChatMessageRequestBuilder()
                .ForBroadcaster(42)
                .WithContent("Pog")
                .AsBot()
                .Build());

        Assert.NotNull(response);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization!.Scheme);
        Assert.Equal("token-123", handler.Requests[0].Headers.Authorization!.Parameter);
    }

    [Fact]
    public void WebhookVerifier_AndParser_Work()
    {
        using var rsa = RSA.Create(2048);
        var publicKeyPem = ExportPublicKeyPem(rsa);
        var headers = new KickWebhookHeaders
        {
            MessageId = "01TESTMESSAGE",
            SubscriptionId = "01TESTSUB",
            MessageTimestamp = "2026-05-27T21:00:00Z",
            EventType = KickWebhookEventNames.ChatMessageSent,
            EventVersion = "1",
        };

        const string json = """
        {"message_id":"m1","content":"hello","sender":{"user_id":7,"username":"tester"},"broadcaster":{"user_id":9,"username":"streamer"},"created_at":"2026-05-27T21:00:00Z"}
        """;

        var payload = Encoding.UTF8.GetBytes($"{headers.MessageId}.{headers.MessageTimestamp}.{json}");
        var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        headers = new KickWebhookHeaders
        {
            MessageId = headers.MessageId,
            SubscriptionId = headers.SubscriptionId,
            MessageTimestamp = headers.MessageTimestamp,
            EventType = headers.EventType,
            EventVersion = headers.EventVersion,
            Signature = Convert.ToBase64String(signature),
        };

        var verifier = new KickWebhookVerifier(publicKeyPem);
        var parsed = KickWebhookParser.Parse(headers.EventType, json);

        Assert.True(verifier.Verify(headers, Encoding.UTF8.GetBytes(json)));
        var chatEvent = Assert.IsType<ChatMessageSentEvent>(parsed);
        Assert.Equal("m1", chatEvent.MessageId);
        Assert.Equal("hello", chatEvent.Content);
        Assert.Equal(7, chatEvent.Sender!.UserId);
    }

    private static string ExportPublicKeyPem(RSA rsa)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-----BEGIN PUBLIC KEY-----");
        builder.AppendLine(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END PUBLIC KEY-----");
        return builder.ToString();
    }

    private sealed class SequenceHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            var content = _responses.Dequeue();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
        }
    }
}
