using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kick;
using Kick.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var commands = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase)
{
    ["help"] = ShowHelpAsync,
    ["app-auth"] = RunAppAuthExampleAsync,
    ["user-auth"] = RunUserAuthExampleAsync,
    ["public-read"] = RunPublicReadExamplesAsync,
    ["channels"] = RunChannelExamplesAsync,
    ["rewards"] = RunRewardExamplesAsync,
    ["chat"] = RunChatExamplesAsync,
    ["events"] = RunEventExamplesAsync,
    ["livestreams"] = RunLivestreamExamplesAsync,
    ["moderation"] = RunModerationExamplesAsync,
    ["public-key"] = RunPublicKeyExampleAsync,
    ["webhooks"] = RunWebhookExamplesAsync,
    ["di"] = RunDependencyInjectionExampleAsync,
    ["all"] = RunAllExamplesAsync,
};

var command = args.Length == 0 ? "help" : args[0];
if (!commands.TryGetValue(command, out var action))
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    await ShowHelpAsync();
    return 1;
}

await action();
return 0;

Task ShowHelpAsync()
{
    Console.WriteLine(
        """
        Kick .NET SDK console examples

        Usage:
          dotnet run --project samples/KickNet.ConsoleExamples -- <command>

        Commands:
          help         Show this message
          app-auth     Demonstrate default client_credentials startup
          user-auth    Demonstrate the rarer browser login flow
          public-read  Categories, users, channels, and leaderboard examples
          channels     Channel lookup and metadata update examples
          rewards      Reward CRUD and redemption workflow examples
          chat         Chat examples; may require a user-authenticated session depending on scopes
          events       Event subscription examples
          livestreams  Livestream listing and stats examples
          moderation   Moderation request examples; may require user auth in practice
          public-key   Fetch the webhook public key
          webhooks     Verify and parse a webhook locally
          di           Register the SDK with dependency injection
          all          Run every example

        Default app-auth environment:
          KICK_CLIENT_ID
          KICK_CLIENT_SECRET

        Optional app-auth environment:
          KICK_BROADCASTER_USER_ID
          KICK_CATEGORY_QUERY
          KICK_CHANNEL_SLUG
          KICK_CHANNEL_REWARD_ID
          KICK_MESSAGE_ID
          KICK_SUBSCRIPTION_ID
          KICK_TARGET_USER_ID
          KICK_REDEMPTION_IDS

        User-auth environment:
          KICK_USER_CLIENT_ID
          KICK_USER_CLIENT_SECRET
          KICK_REDIRECT_URI
        """);

    return Task.CompletedTask;
}

async Task RunAllExamplesAsync()
{
    await RunAppAuthExampleAsync();
    await RunUserAuthExampleAsync();
    await RunPublicReadExamplesAsync();
    await RunChannelExamplesAsync();
    await RunRewardExamplesAsync();
    await RunChatExamplesAsync();
    await RunEventExamplesAsync();
    await RunLivestreamExamplesAsync();
    await RunModerationExamplesAsync();
    await RunPublicKeyExampleAsync();
    await RunWebhookExamplesAsync();
    await RunDependencyInjectionExampleAsync();
}

Task RunAppAuthExampleAsync()
{
    Section("App Auth");

    if (!TryCreateAppSession(out var session))
    {
        ExplainMissingAppCredentials();
        Console.WriteLine(
            """
            var session = KickSdk.CreateAppSession(new KickAppCredentials
            {
                ClientId = "<client-id>",
                ClientSecret = "<client-secret>",
            });

            var kick = session.Client;
            """);
        return Task.CompletedTask;
    }

    Console.WriteLine("Created default app-authenticated session.");
    Console.WriteLine($"Client type: {session.Client.GetType().FullName}");
    return Task.CompletedTask;
}

Task RunUserAuthExampleAsync()
{
    Section("User Auth");

    var clientId = Env("KICK_USER_CLIENT_ID") ?? Env("KICK_CLIENT_ID") ?? "<client-id>";
    var clientSecret = Env("KICK_USER_CLIENT_SECRET") ?? Env("KICK_CLIENT_SECRET") ?? "<client-secret>";
    var redirectUri = Env("KICK_REDIRECT_URI") ?? "https://localhost/callback";

    var auth = KickSdk.CreateUserAuthClient(new KickUserAuthOptions
    {
        ClientId = clientId,
        ClientSecret = clientSecret,
        RedirectUri = redirectUri,
        DefaultScopes = [KickScopes.UserRead, KickScopes.ChannelRead],
    });

    var login = auth.CreateLoginRequest();
    Console.WriteLine($"Code verifier : {login.CodeVerifier}");
    Console.WriteLine($"Code challenge: {login.CodeChallenge}");
    Console.WriteLine($"State         : {login.State}");
    Console.WriteLine($"Authorize URL : {login.AuthorizationUri}");
    Console.WriteLine();
    Console.WriteLine("Exchange the callback code with:");
    Console.WriteLine(
        $$"""
        var session = await auth.ExchangeAuthorizationCodeAsync(
            code: "<authorization-code>",
            codeVerifier: "{{login.CodeVerifier}}");

        var kick = session.Client;
        """);
    return Task.CompletedTask;
}

async Task RunPublicReadExamplesAsync()
{
    Section("Public Read");

    var categoryQuery = Env("KICK_CATEGORY_QUERY") ?? "rust";
    var channelSlug = Env("KICK_CHANNEL_SLUG") ?? "xqc";
    DumpObject(new GetCategoriesRequest { Query = categoryQuery, Page = 1 });
    DumpObject(new GetCategoriesV2Request { Limit = 5, Names = ["Rust"] });
    DumpObject(new GetUsersRequest { Ids = [1, 2] });
    DumpObject(new GetChannelsRequest { Slugs = [channelSlug] });
    DumpObject(new GetKicksLeaderboardRequest { Top = 5 });

    if (!TryCreateAppSession(out var session))
    {
        ExplainMissingAppCredentials();
        return;
    }

    var kick = session.Client;
    var categories = await kick.Categories.GetAsync(new GetCategoriesRequest { Query = categoryQuery, Page = 1 });
    Console.WriteLine($"Categories matching '{categoryQuery}': {categories?.Data?.Count ?? 0}");
}

async Task RunChannelExamplesAsync()
{
    Section("Channels");

    var broadcasterId = EnvInt("KICK_BROADCASTER_USER_ID") ?? 1;
    var channelSlug = Env("KICK_CHANNEL_SLUG") ?? "xqc";
    var updateRequest = new UpdateChannelRequestBuilder()
        .WithStreamTitle("Kick.NET SDK example stream title")
        .AddCustomTag("sdk")
        .AddCustomTag("dotnet")
        .Build();

    DumpObject(new GetChannelsRequest
    {
        BroadcasterUserIds = [broadcasterId],
        Slugs = [channelSlug],
    });
    DumpObject(updateRequest);

    if (!TryCreateAppSession(out var session))
    {
        ExplainMissingAppCredentials();
        return;
    }

    var channels = await session.Client.Channels.GetAsync(new GetChannelsRequest
    {
        BroadcasterUserIds = [broadcasterId],
        Slugs = [channelSlug],
    });
    Console.WriteLine($"Fetched channel records: {channels?.Data?.Count ?? 0}");
}

async Task RunRewardExamplesAsync()
{
    Section("Rewards");

    var rewardId = Env("KICK_CHANNEL_REWARD_ID") ?? "<reward-id>";
    DumpObject(new CreateChannelRewardRequestBuilder()
        .WithTitle("Song Request")
        .WithCost(250)
        .WithDescription("Paste a valid URL.")
        .WithBackgroundColor("#00e701")
        .RequiresUserInput()
        .IsEnabled()
        .Build());
    DumpObject(new UpdateChannelRewardRequestBuilder()
        .WithTitle("Priority Song Request")
        .WithCost(500)
        .IsEnabled()
        .RequiresUserInput()
        .Build());
    DumpObject(new GetRewardRedemptionsRequest
    {
        RewardId = rewardId == "<reward-id>" ? null : rewardId,
        Status = "pending",
    });

    if (!TryCreateAppSession(out var session))
    {
        ExplainMissingAppCredentials();
        return;
    }

    var rewards = await session.Client.ChannelRewards.GetAsync();
    Console.WriteLine($"Current rewards: {rewards?.Data?.Count ?? 0}");
}

Task RunChatExamplesAsync()
{
    Section("Chat");
    Console.WriteLine("This sample prints chat request shapes only.");
    Console.WriteLine("In real deployments, confirm the endpoint/scopes with a user-authenticated session when required.");

    var broadcasterId = EnvInt("KICK_BROADCASTER_USER_ID") ?? 1;
    DumpObject(new PostChatMessageRequestBuilder()
        .ForBroadcaster(broadcasterId)
        .WithContent("Kick.NET SDK says hello from the console sample.")
        .AsBot()
        .Build());
    return Task.CompletedTask;
}

Task RunEventExamplesAsync()
{
    Section("Events");

    var broadcasterId = EnvInt("KICK_BROADCASTER_USER_ID") ?? 1;
    DumpObject(new GetEventSubscriptionsRequest { BroadcasterUserId = broadcasterId });
    DumpObject(new CreateEventSubscriptionsRequestBuilder()
        .ForBroadcaster(broadcasterId)
        .UsingWebhook()
        .AddEvent(KickWebhookEventNames.ChatMessageSent)
        .AddEvent(KickWebhookEventNames.ChannelFollowed)
        .Build());
    return Task.CompletedTask;
}

async Task RunLivestreamExamplesAsync()
{
    Section("Livestreams");
    DumpObject(new GetLivestreamsRequest
    {
        BroadcasterUserIds = [EnvInt("KICK_BROADCASTER_USER_ID") ?? 1],
        Limit = 10,
        Sort = "viewer_count",
        Language = "en",
    });

    if (!TryCreateAppSession(out var session))
    {
        ExplainMissingAppCredentials();
        return;
    }

    var stats = await session.Client.Livestreams.GetStatsAsync();
    Console.WriteLine($"Livestream total count: {stats?.Data?.TotalCount ?? 0}");
}

Task RunModerationExamplesAsync()
{
    Section("Moderation");
    Console.WriteLine("Moderation often depends on user context/scopes. Treat this as a user-auth candidate workflow.");
    DumpObject(new BanUserRequestBuilder()
        .ForBroadcaster(EnvInt("KICK_BROADCASTER_USER_ID") ?? 1)
        .ForUser(EnvInt("KICK_TARGET_USER_ID") ?? 2)
        .WithReason("Kick.NET sample moderation action")
        .TimeoutForMinutes(10)
        .Build());
    return Task.CompletedTask;
}

async Task RunPublicKeyExampleAsync()
{
    Section("Public Key");

    if (!TryCreateAppSession(out var session))
    {
        ExplainMissingAppCredentials();
        return;
    }

    var publicKey = await session.Client.PublicKey.GetAsync();
    Console.WriteLine(publicKey is null ? "No public key returned." : publicKey);
}

Task RunWebhookExamplesAsync()
{
    Section("Webhooks");

    using var rsa = RSA.Create(2048);
    var publicKeyPem = ExportPublicKeyPem(rsa);
    var body = """
        {"message_id":"m1","content":"hello chat","sender":{"user_id":7,"username":"viewer"},"broadcaster":{"user_id":99,"username":"streamer"},"created_at":"2026-05-28T10:00:00Z"}
        """;

    var headers = new KickWebhookHeaders
    {
        MessageId = "01HXEXAMPLEMESSAGE",
        SubscriptionId = "01HXEXAMPLESUBSCRIPTION",
        MessageTimestamp = "2026-05-28T10:00:00Z",
        EventType = KickWebhookEventNames.ChatMessageSent,
        EventVersion = "1",
        Signature = Convert.ToBase64String(
            rsa.SignData(
                Encoding.UTF8.GetBytes($"01HXEXAMPLEMESSAGE.2026-05-28T10:00:00Z.{body}"),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1)),
    };

    var verifier = new KickWebhookVerifier(publicKeyPem);
    var parsed = KickWebhookParser.Parse(headers.EventType, body);

    Console.WriteLine($"Signature valid: {verifier.Verify(headers, Encoding.UTF8.GetBytes(body))}");
    Console.WriteLine($"Parsed type    : {parsed.GetType().Name}");
    DumpObject(parsed);
    return Task.CompletedTask;
}

Task RunDependencyInjectionExampleAsync()
{
    Section("Dependency Injection");

    var services = new ServiceCollection();
    services.AddKickAppSession(new KickAppCredentials
    {
        ClientId = "sample-client",
        ClientSecret = "sample-secret",
    });
    services.AddKickUserAuth(new KickUserAuthOptions
    {
        ClientId = "sample-client",
        ClientSecret = "sample-secret",
        RedirectUri = "https://localhost/callback",
        DefaultScopes = [KickScopes.UserRead],
    });

    using var provider = services.BuildServiceProvider();
    var session = provider.GetRequiredService<KickAppSession>();
    var userAuth = provider.GetRequiredService<KickUserAuthClient>();

    Console.WriteLine($"Resolved session: {session.GetType().FullName}");
    Console.WriteLine($"Resolved auth   : {userAuth.GetType().FullName}");
    return Task.CompletedTask;
}

bool TryCreateAppSession(out KickAppSession session)
{
    var clientId = Env("KICK_CLIENT_ID");
    var clientSecret = Env("KICK_CLIENT_SECRET");
    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
    {
        session = null!;
        return false;
    }

    session = KickSdk.CreateAppSession(new KickAppCredentials
    {
        ClientId = clientId,
        ClientSecret = clientSecret,
    });
    return true;
}

static void ExplainMissingAppCredentials()
{
    Console.WriteLine("Set KICK_CLIENT_ID and KICK_CLIENT_SECRET to run the default app-auth examples.");
}

static string? Env(string name) => Environment.GetEnvironmentVariable(name);

static int? EnvInt(string name)
{
    var value = Env(name);
    return int.TryParse(value, out var result) ? result : null;
}

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', title.Length));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', title.Length));
}

static void DumpObject<T>(T value)
{
    Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}

static string ExportPublicKeyPem(RSA rsa)
{
    var builder = new StringBuilder();
    builder.AppendLine("-----BEGIN PUBLIC KEY-----");
    builder.AppendLine(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks));
    builder.AppendLine("-----END PUBLIC KEY-----");
    return builder.ToString();
}
