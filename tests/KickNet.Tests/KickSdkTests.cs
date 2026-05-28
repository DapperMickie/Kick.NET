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
    public void KickClient_ExposesExperimentalMediaClients()
    {
        var client = new KickClient();

        Assert.NotNull(client.Experimental);
        Assert.NotNull(client.Experimental.Videos);
        Assert.NotNull(client.Experimental.Clips);
    }

    [Fact]
    public async Task ExperimentalClient_ThrowsWhenNotEnabled()
    {
        var client = new KickClient();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.Experimental.Videos.GetByIdAsync("123"));
    }

    [Fact]
    public async Task ExperimentalVideos_GetById_UsesWebsiteBaseUri()
    {
        var handler = new SequenceHandler(
            """
            {"id":123,"uuid":"video-uuid","slug":"video-slug","title":"Test VOD","thumbnail":"https://example.test/thumb.jpg","duration":3600,"views":42,"language":"en","created_at":"2026-05-28T10:00:00Z","channel":{"id":7,"slug":"xqc","username":"xQc"}}
            """);

        using var httpClient = new HttpClient(handler);
        var client = CreateExperimentalClient(httpClient);

        var video = await client.Experimental.Videos.GetByIdAsync("123");

        Assert.NotNull(video);
        Assert.Equal(123, video.Id);
        Assert.Equal("video-uuid", video.Uuid);
        Assert.Equal("Test VOD", video.Title);
        Assert.Equal("xqc", video.Channel!.Slug);
        Assert.Equal("https://kick.com/api/v1/video/123", handler.Requests[0].RequestUri!.ToString());
        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task ExperimentalVideos_GetLatestByChannel_UsesExpectedRoute()
    {
        var handler = new SequenceHandler(
            """
            [{"id":123,"title":"Latest VOD"}]
            """);

        using var httpClient = new HttpClient(handler);
        var client = CreateExperimentalClient(httpClient);

        var videos = await client.Experimental.Videos.GetLatestByChannelAsync("xqc");

        Assert.NotNull(videos);
        Assert.Single(videos);
        Assert.Equal("Latest VOD", videos[0].Title);
        Assert.Equal("https://kick.com/api/v2/channels/xqc/videos/latest", handler.Requests[0].RequestUri!.ToString());
        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task ExperimentalClips_GetBySlug_UsesExpectedRoute()
    {
        var handler = new SequenceHandler(
            """
            {"id":"clip-id","slug":"clip-slug","title":"Test Clip","video_url":"https://clips.example.test/clip.m3u8","duration":30}
            """);

        using var httpClient = new HttpClient(handler);
        var client = CreateExperimentalClient(httpClient);

        var clip = await client.Experimental.Clips.GetBySlugAsync("clip-slug");

        Assert.NotNull(clip);
        Assert.Equal("clip-id", clip.Id);
        Assert.Equal("https://clips.example.test/clip.m3u8", clip.VideoUrl);
        Assert.Equal("https://kick.com/api/v2/clips/clip-slug", handler.Requests[0].RequestUri!.ToString());
        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task ExperimentalClips_GetByChannel_AddsQuery()
    {
        var handler = new SequenceHandler(
            """
            {"data":[{"id":"clip-id","title":"Channel Clip"}]}
            """);

        using var httpClient = new HttpClient(handler);
        var client = CreateExperimentalClient(httpClient);

        var clips = await client.Experimental.Clips.GetByChannelAsync(new GetChannelWebsiteClipsRequest
        {
            Channel = "xqc",
            Page = 2,
            Limit = 25,
            Sort = "date",
        });

        Assert.NotNull(clips);
        Assert.Single(clips);
        Assert.Equal("Channel Clip", clips[0].Title);
        Assert.Equal("https://kick.com/api/v2/channels/xqc/clips?page=2&limit=25&sort=date", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task ExperimentalClips_GetGlobal_AddsQuery()
    {
        var handler = new SequenceHandler(
            """
            {"data":{"data":[{"id":"clip-id","title":"Global Clip"}]}}
            """);

        using var httpClient = new HttpClient(handler);
        var client = CreateExperimentalClient(httpClient);

        var clips = await client.Experimental.Clips.GetAsync(new GetWebsiteClipsRequest
        {
            Page = 1,
            Limit = 50,
            Sort = "views",
        });

        Assert.NotNull(clips);
        Assert.Single(clips);
        Assert.Equal("Global Clip", clips[0].Title);
        Assert.Equal("https://kick.com/api/v2/clips?page=1&limit=50&sort=views", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task ExperimentalClips_GetByCategory_UsesExpectedRoute()
    {
        var handler = new SequenceHandler(
            """
            [{"id":"clip-id","title":"Category Clip"}]
            """);

        using var httpClient = new HttpClient(handler);
        var client = CreateExperimentalClient(httpClient);

        var clips = await client.Experimental.Clips.GetByCategoryAsync(new GetCategoryWebsiteClipsRequest
        {
            Category = "software-development",
        });

        Assert.NotNull(clips);
        Assert.Single(clips);
        Assert.Equal("Category Clip", clips[0].Title);
        Assert.Equal("https://kick.com/api/v2/categories/software-development/clips", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task ExperimentalMediaClients_ValidateRequiredArguments()
    {
        var client = CreateExperimentalClient(new HttpClient(new SequenceHandler()));

        await Assert.ThrowsAsync<ArgumentException>(() => client.Experimental.Videos.GetByIdAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Experimental.Videos.GetLatestByChannelAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Experimental.Clips.GetBySlugAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Experimental.Clips.GetInfoAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Experimental.Clips.GetByChannelAsync(new GetChannelWebsiteClipsRequest()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Experimental.Clips.GetByCategoryAsync(new GetCategoryWebsiteClipsRequest()));
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

    private static KickClient CreateExperimentalClient(HttpClient httpClient)
    {
        return new KickClient(
            httpClient,
            options: new KickClientOptions
            {
                WebsiteBaseUri = new Uri("https://kick.com/"),
                EnableExperimentalWebsiteApi = true,
            });
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
