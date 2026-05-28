using System.Net;
using System.Text;
using System.Text.Json;
using Kick;
using Microsoft.Extensions.Options;
using Spectre.Console;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KickBotSampleOptions>(builder.Configuration.GetSection("Kick"));
builder.Services.AddSingleton<BotLoginState>();
builder.Services.AddSingleton<BotSubscriptionStore>();
builder.Services.AddSingleton<BotUserSessionStore>();
builder.Services.AddSingleton(static sp =>
{
    var options = sp.GetRequiredService<IOptions<KickBotSampleOptions>>().Value;
    return KickSdk.CreateUserAuthClient(new KickUserAuthOptions
    {
        ClientId = options.ClientId,
        ClientSecret = options.ClientSecret,
        RedirectUri = options.RedirectUri,
        DefaultScopes = [KickScopes.UserRead, KickScopes.EventsSubscribe],
    });
});

var app = builder.Build();

app.MapGet("/", (IOptions<KickBotSampleOptions> options, BotUserSessionStore sessionStore, BotSubscriptionStore subscriptions) =>
{
    var tokenInfo = sessionStore.CurrentSession?.TokenInfo;
    var model = new SampleStatusViewModel(
        options.Value,
        tokenInfo is not null,
        !string.IsNullOrWhiteSpace(tokenInfo?.RefreshToken),
        tokenInfo?.Scope,
        tokenInfo?.ExpiresAtUtc,
        subscriptions.SubscriptionIds.ToArray());

    return Results.Content(RenderHomePage(model), "text/html; charset=utf-8");
});

app.MapGet("/health", (BotUserSessionStore sessionStore, BotSubscriptionStore subscriptions) =>
{
    var tokenInfo = sessionStore.CurrentSession?.TokenInfo;
    return Results.Json(new
    {
        logged_in = tokenInfo is not null,
        has_refresh_token = !string.IsNullOrWhiteSpace(tokenInfo?.RefreshToken),
        tokenInfo?.Scope,
        expires_at_utc = tokenInfo?.ExpiresAtUtc,
        subscription_ids = subscriptions.SubscriptionIds,
    });
});

app.MapGet("/login", (
    IOptions<KickBotSampleOptions> options,
    BotLoginState loginState,
    KickUserAuthClient authClient) =>
{
    ValidateOptions(options.Value);

    var redirect = Uri.TryCreate(options.Value.RedirectUri, UriKind.Absolute, out var redirectUri) &&
                   redirectUri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        ? "127.0.0.1"
        : null;

    var login = authClient.CreateLoginRequest(sacrificialRedirect: redirect);
    loginState.Set(login.State, login.CodeVerifier);

    AnsiConsole.MarkupLine("[grey]Redirecting browser to KICK OAuth authorization endpoint...[/]");
    AnsiConsole.MarkupLine($"[blue underline]{Markup.Escape(login.AuthorizationUri.ToString())}[/]");

    return Results.Redirect(login.AuthorizationUri.ToString());
});

app.MapGet("/oauth/callback", async (
    HttpContext httpContext,
    IOptions<KickBotSampleOptions> options,
    BotLoginState loginState,
    BotUserSessionStore sessionStore,
    BotSubscriptionStore subscriptionStore,
    KickUserAuthClient authClient,
    CancellationToken cancellationToken) =>
{
    ValidateOptions(options.Value);

    var code = httpContext.Request.Query["code"].ToString();
    var state = httpContext.Request.Query["state"].ToString();
    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
    {
        return Results.BadRequest("Missing 'code' or 'state' query string.");
    }

    if (!loginState.TryConsume(state, out var codeVerifier))
    {
        return Results.BadRequest("Invalid or expired OAuth state.");
    }

    var session = await authClient.ExchangeAuthorizationCodeAsync(code, codeVerifier, cancellationToken);
    sessionStore.Set(session);

    var subscriptionResponse = await session.Client.Events.CreateSubscriptionsAsync(
        new CreateEventSubscriptionsRequestBuilder()
            .UsingWebhook()
            .AddEvent(KickWebhookEventNames.ChatMessageSent)
            .Build(),
        cancellationToken);

    subscriptionStore.Replace(
        subscriptionResponse?.Data?
            .Select(static x => x.SubscriptionId)
            .OfType<string>() ??
        []);

    WriteLoginSuccess(session.TokenInfo, subscriptionStore);

    return Results.Content(
        """
        <html>
          <body style="font-family: sans-serif; max-width: 720px; margin: 3rem auto;">
            <h1>Bot login complete</h1>
            <p>This user-auth sample stored the user access token in memory and subscribed the logged-in bot to <code>chat.message.sent</code>.</p>
            <p>Return to <a href="/">the home page</a> and keep this process running so KICK can post webhooks to <code>/webhooks/kick</code>.</p>
          </body>
        </html>
        """,
        "text/html; charset=utf-8");
});

app.MapPost("/webhooks/kick", async (
    HttpContext httpContext,
    IOptions<KickBotSampleOptions> options,
    CancellationToken cancellationToken) =>
{
    var rawBody = await ReadRawBodyAsync(httpContext.Request, cancellationToken);
    var headers = TryReadWebhookHeaders(httpContext.Request);
    if (headers is null)
    {
        return Results.BadRequest("Missing one or more KICK webhook headers.");
    }

    var verifier = new KickWebhookVerifier(
        string.IsNullOrWhiteSpace(options.Value.WebhookPublicKeyPem)
            ? KickWebhookVerifier.DefaultPublicKeyPem
            : options.Value.WebhookPublicKeyPem);

    if (!verifier.Verify(headers, Encoding.UTF8.GetBytes(rawBody)))
    {
        AnsiConsole.MarkupLine("[red]Rejected webhook with invalid signature.[/]");
        return Results.StatusCode((int)HttpStatusCode.Unauthorized);
    }

    PrettyPrintWebhook(headers, KickWebhookParser.Parse(headers.EventType, rawBody));
    return Results.Ok();
});

app.MapPost("/logout", async (
    BotUserSessionStore sessionStore,
    BotSubscriptionStore subscriptionStore,
    CancellationToken cancellationToken) =>
{
    if (sessionStore.CurrentSession is not null)
    {
        await sessionStore.CurrentSession.RevokeAsync(cancellationToken);
    }

    sessionStore.Clear();
    subscriptionStore.Clear();
    AnsiConsole.MarkupLine("[yellow]Local bot session cleared.[/]");

    return Results.Redirect("/");
});

app.Run();

static void ValidateOptions(KickBotSampleOptions options)
{
    if (string.IsNullOrWhiteSpace(options.ClientId) ||
        string.IsNullOrWhiteSpace(options.ClientSecret) ||
        string.IsNullOrWhiteSpace(options.RedirectUri))
    {
        throw new InvalidOperationException(
            "Set Kick:ClientId, Kick:ClientSecret, and Kick:RedirectUri before running the bot login sample.");
    }
}

static async Task<string> ReadRawBodyAsync(HttpRequest request, CancellationToken cancellationToken)
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    return await reader.ReadToEndAsync(cancellationToken);
}

static KickWebhookHeaders? TryReadWebhookHeaders(HttpRequest request)
{
    var messageId = request.Headers["Kick-Event-Message-Id"].ToString();
    var subscriptionId = request.Headers["Kick-Event-Subscription-Id"].ToString();
    var signature = request.Headers["Kick-Event-Signature"].ToString();
    var timestamp = request.Headers["Kick-Event-Message-Timestamp"].ToString();
    var eventType = request.Headers["Kick-Event-Type"].ToString();
    var version = request.Headers["Kick-Event-Version"].ToString();

    if (string.IsNullOrWhiteSpace(messageId) ||
        string.IsNullOrWhiteSpace(subscriptionId) ||
        string.IsNullOrWhiteSpace(signature) ||
        string.IsNullOrWhiteSpace(timestamp) ||
        string.IsNullOrWhiteSpace(eventType) ||
        string.IsNullOrWhiteSpace(version))
    {
        return null;
    }

    return new KickWebhookHeaders
    {
        MessageId = messageId,
        SubscriptionId = subscriptionId,
        Signature = signature,
        MessageTimestamp = timestamp,
        EventType = eventType,
        EventVersion = version,
    };
}

static void WriteLoginSuccess(UserTokenInfo tokenInfo, BotSubscriptionStore subscriptionStore)
{
    var table = new Table().RoundedBorder();
    table.AddColumn("Field");
    table.AddColumn("Value");
    table.AddRow("Scope", tokenInfo.Scope ?? "(none)");
    table.AddRow("Expires", tokenInfo.ExpiresAtUtc?.ToString("u") ?? "(unknown)");
    table.AddRow("Subscriptions", subscriptionStore.SubscriptionIds.Count == 0 ? "(none returned)" : string.Join(", ", subscriptionStore.SubscriptionIds));

    AnsiConsole.Write(new Panel(table)
        .Header("[green]Bot Login Complete[/]")
        .Border(BoxBorder.Double));
}

static void PrettyPrintWebhook(KickWebhookHeaders headers, IKickWebhookEvent webhookEvent)
{
    switch (webhookEvent)
    {
        case ChatMessageSentEvent chatMessage:
        {
            var rows = new Grid();
            rows.AddColumn();
            rows.AddColumn();
            rows.AddRow("[grey]Message Id[/]", Markup.Escape(chatMessage.MessageId ?? headers.MessageId));
            rows.AddRow("[grey]At[/]", Markup.Escape(chatMessage.CreatedAt?.ToString("u") ?? headers.MessageTimestamp));
            rows.AddRow("[grey]Broadcaster[/]", Markup.Escape(chatMessage.Broadcaster?.Username ?? "(unknown)"));
            rows.AddRow("[grey]Sender[/]", Markup.Escape(chatMessage.Sender?.Username ?? "(unknown)"));
            rows.AddRow("[grey]Content[/]", Markup.Escape(chatMessage.Content ?? string.Empty));

            AnsiConsole.Write(new Panel(rows)
                .Header("[deepskyblue1]chat.message.sent[/]")
                .Border(BoxBorder.Rounded));
            break;
        }
        default:
            AnsiConsole.Write(new Panel(JsonSerializer.Serialize(webhookEvent, new JsonSerializerOptions { WriteIndented = true }))
                .Header($"[yellow]{Markup.Escape(headers.EventType)}[/]")
                .Border(BoxBorder.Rounded));
            break;
    }
}

static string RenderHomePage(SampleStatusViewModel model)
{
    var subscriptions = model.SubscriptionIds.Length == 0
        ? "<li>None yet</li>"
        : string.Join(Environment.NewLine, model.SubscriptionIds.Select(static id => $"<li><code>{WebUtility.HtmlEncode(id)}</code></li>"));

    var webhookTarget = string.IsNullOrWhiteSpace(model.Options.WebhookPublicUrl)
        ? "/webhooks/kick"
        : $"{model.Options.WebhookPublicUrl.TrimEnd('/')}/webhooks/kick";

    return $$"""
    <html>
      <head>
        <title>Kick.NET Bot Login Sample</title>
        <style>
          body { font-family: system-ui, sans-serif; max-width: 860px; margin: 2rem auto; line-height: 1.5; }
          code, pre { background: #f5f5f5; padding: 0.15rem 0.35rem; border-radius: 4px; }
          .card { border: 1px solid #ddd; border-radius: 10px; padding: 1rem 1.25rem; margin-bottom: 1rem; }
          .actions { display: flex; gap: 0.75rem; flex-wrap: wrap; }
          .button { display: inline-block; padding: 0.7rem 1rem; background: #16a34a; color: white; border-radius: 8px; text-decoration: none; }
          .button.secondary { background: #2563eb; }
          .button.warning { background: #b45309; border: none; cursor: pointer; }
        </style>
      </head>
      <body>
        <h1>Kick.NET Bot Login + Webhook Sample</h1>
        <p>This sample demonstrates the exceptional user-auth path. The normal SDK startup path is app auth with <code>client_id</code> and <code>client_secret</code>; this app specifically shows browser login, in-memory user tokens, webhook subscription, and verified chat logging.</p>

        <div class="card">
          <h2>1. KICK app setup</h2>
          <p>Configure your KICK app with this redirect URI:</p>
          <pre>{{WebUtility.HtmlEncode(model.Options.RedirectUri ?? "(set Kick:RedirectUri)")}}</pre>
          <p>Configure your app's webhook URL to point at:</p>
          <pre>{{WebUtility.HtmlEncode(webhookTarget)}}</pre>
          <p>If you are running locally, expose this app publicly through a tunnel such as ngrok or Cloudflare Tunnel and use that public URL for both KICK app settings.</p>
        </div>

        <div class="card">
          <h2>2. Session status</h2>
          <ul>
            <li>User access token present: <strong>{{model.HasAccessToken}}</strong></li>
            <li>Refresh token present: <strong>{{model.HasRefreshToken}}</strong></li>
            <li>Scopes: <code>{{WebUtility.HtmlEncode(model.Scope ?? "(none)")}}</code></li>
            <li>Expires at UTC: <code>{{WebUtility.HtmlEncode(model.ExpiresAtUtc?.ToString("u") ?? "(unknown)")}}</code></li>
          </ul>
          <h3>Subscriptions</h3>
          <ul>
            {{subscriptions}}
          </ul>
        </div>

        <div class="card">
          <h2>3. Actions</h2>
          <div class="actions">
            <a class="button" href="/login">Log In As Bot</a>
            <a class="button secondary" href="/health">View JSON Status</a>
            <form method="post" action="/logout">
              <button class="button warning" type="submit">Clear Session</button>
            </form>
          </div>
        </div>
      </body>
    </html>
    """;
}

sealed record SampleStatusViewModel(
    KickBotSampleOptions Options,
    bool HasAccessToken,
    bool HasRefreshToken,
    string? Scope,
    DateTimeOffset? ExpiresAtUtc,
    string[] SubscriptionIds);

sealed class KickBotSampleOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public string? WebhookPublicUrl { get; init; }
    public string? WebhookPublicKeyPem { get; init; }
}

sealed class BotLoginState
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _pending = [];

    public void Set(string state, string codeVerifier)
    {
        lock (_gate)
        {
            _pending[state] = codeVerifier;
        }
    }

    public bool TryConsume(string state, out string codeVerifier)
    {
        lock (_gate)
        {
            if (_pending.Remove(state, out codeVerifier!))
            {
                return true;
            }
        }

        codeVerifier = string.Empty;
        return false;
    }
}

sealed class BotSubscriptionStore
{
    private readonly Lock _gate = new();
    private List<string> _subscriptionIds = [];

    public IReadOnlyList<string> SubscriptionIds
    {
        get
        {
            lock (_gate)
            {
                return _subscriptionIds.ToArray();
            }
        }
    }

    public void Replace(IEnumerable<string> ids)
    {
        lock (_gate)
        {
            _subscriptionIds = ids.Where(static id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _subscriptionIds.Clear();
        }
    }
}

sealed class BotUserSessionStore
{
    public KickUserSession? CurrentSession { get; private set; }

    public void Set(KickUserSession session)
    {
        CurrentSession = session;
    }

    public void Clear()
    {
        CurrentSession = null;
    }
}
