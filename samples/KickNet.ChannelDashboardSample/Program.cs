using System.Net;
using Kick;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ChannelDashboardOptions>(builder.Configuration.GetSection("Kick"));

var app = builder.Build();

app.MapGet("/", async (IOptions<ChannelDashboardOptions> options, CancellationToken cancellationToken) =>
{
    var config = options.Value;
    if (!TryCreateClient(config, out var kick))
    {
        return Results.Content(RenderSetupPage(), "text/html; charset=utf-8");
    }

    var channels = await kick.Channels.GetAsync(new GetChannelsRequest
    {
        BroadcasterUserIds = config.BroadcasterUserId > 0 ? [config.BroadcasterUserId] : null,
        Slugs = string.IsNullOrWhiteSpace(config.ChannelSlug) ? null : [config.ChannelSlug],
    }, cancellationToken);

    var livestreams = await kick.Livestreams.GetAsync(new GetLivestreamsRequest
    {
        BroadcasterUserIds = config.BroadcasterUserId > 0 ? [config.BroadcasterUserId] : null,
        Limit = 10,
        Language = config.DiscoverLanguage,
        Sort = "viewer_count",
    }, cancellationToken);

    var stats = await kick.Livestreams.GetStatsAsync(cancellationToken);

    return Results.Content(RenderHomePage(config, channels?.Data ?? [], livestreams?.Data ?? [], stats?.Data), "text/html; charset=utf-8");
}).AllowAnonymous();

app.MapPost("/channel/update", async (HttpRequest request, IOptions<ChannelDashboardOptions> options, CancellationToken cancellationToken) =>
{
    if (!TryCreateClient(options.Value, out var kick))
    {
        return Results.Content(RenderSetupPage(), "text/html; charset=utf-8");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var categoryRaw = form["categoryId"].ToString();
    var title = form["streamTitle"].ToString();
    var tagsRaw = form["customTags"].ToString();

    var builder = new UpdateChannelRequestBuilder()
        .WithStreamTitle(title);

    if (int.TryParse(categoryRaw, out var categoryId) && categoryId > 0)
    {
        builder.WithCategoryId(categoryId);
    }

    foreach (var tag in tagsRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        builder.AddCustomTag(tag);
    }

    await kick.Channels.UpdateAsync(builder.Build(), cancellationToken);
    return Results.Redirect("/");
}).AllowAnonymous();

app.MapGet("/discover", async (HttpRequest request, IOptions<ChannelDashboardOptions> options, CancellationToken cancellationToken) =>
{
    if (!TryCreateClient(options.Value, out var kick))
    {
        return Results.Content(RenderSetupPage(), "text/html; charset=utf-8");
    }

    var language = request.Query["language"].ToString();
    var categoryIdRaw = request.Query["categoryId"].ToString();
    var limitRaw = request.Query["limit"].ToString();
    var slug = request.Query["slug"].ToString();

    var response = await kick.Livestreams.GetAsync(new GetLivestreamsRequest
    {
        BroadcasterUserIds = null,
        CategoryId = int.TryParse(categoryIdRaw, out var categoryId) ? categoryId : null,
        Language = string.IsNullOrWhiteSpace(language) ? null : language,
        Limit = int.TryParse(limitRaw, out var limit) ? limit : 10,
        Sort = "viewer_count",
    }, cancellationToken);

    return Results.Content(RenderDiscoverPage(response?.Data ?? [], slug), "text/html; charset=utf-8");
}).AllowAnonymous();

app.Run();

static string RenderHomePage(ChannelDashboardOptions options, IReadOnlyList<Channel> channels, IReadOnlyList<Livestream> livestreams, LivestreamStats? stats)
{
    var channelRows = channels.Count == 0
        ? "<tr><td colspan=\"6\">No channels returned.</td></tr>"
        : string.Join(Environment.NewLine, channels.Select(static channel => $$"""
            <tr>
              <td>{{channel.BroadcasterUserId}}</td>
              <td>{{WebUtility.HtmlEncode(channel.Slug ?? "")}}</td>
              <td>{{WebUtility.HtmlEncode(channel.StreamTitle ?? "")}}</td>
              <td>{{WebUtility.HtmlEncode(channel.Category?.Name ?? "")}}</td>
              <td>{{channel.Stream?.ViewerCount?.ToString() ?? ""}}</td>
              <td>{{WebUtility.HtmlEncode(channel.Stream?.Language ?? "")}}</td>
            </tr>
            """));

    var livestreamRows = livestreams.Count == 0
        ? "<tr><td colspan=\"5\">No livestreams returned.</td></tr>"
        : string.Join(Environment.NewLine, livestreams.Select(static stream => $$"""
            <tr>
              <td>{{WebUtility.HtmlEncode(stream.Slug ?? "")}}</td>
              <td>{{WebUtility.HtmlEncode(stream.StreamTitle ?? "")}}</td>
              <td>{{WebUtility.HtmlEncode(stream.Category?.Name ?? "")}}</td>
              <td>{{stream.ViewerCount?.ToString() ?? ""}}</td>
              <td>{{WebUtility.HtmlEncode(stream.Language ?? "")}}</td>
            </tr>
            """));

    return $$"""
    <html>
      <head>
        <title>Kick.NET Channel Dashboard Sample</title>
        <style>
          body { font-family: system-ui, sans-serif; max-width: 1100px; margin: 2rem auto; line-height: 1.4; }
          h1, h2 { margin-bottom: 0.5rem; }
          .card { border: 1px solid #ddd; border-radius: 10px; padding: 1rem 1.25rem; margin-bottom: 1rem; }
          table { border-collapse: collapse; width: 100%; }
          th, td { border-bottom: 1px solid #e7e7e7; text-align: left; padding: 0.6rem; }
          input { width: 100%; padding: 0.65rem; margin-bottom: 0.75rem; box-sizing: border-box; }
          button { background: #0f766e; border: none; color: white; padding: 0.75rem 1rem; border-radius: 8px; cursor: pointer; }
          code, pre { background: #f5f5f5; padding: 0.15rem 0.35rem; border-radius: 4px; }
        </style>
      </head>
      <body>
        <h1>Kick.NET Channel Dashboard Sample</h1>
        <p>This sample focuses on channel operations: inspect channel state, view livestreams, and post metadata updates through the SDK.</p>

        <div class="card">
          <h2>Configured target</h2>
          <p><strong>Broadcaster user ID:</strong> {{options.BroadcasterUserId}}</p>
          <p><strong>Channel slug:</strong> <code>{{WebUtility.HtmlEncode(options.ChannelSlug ?? "")}}</code></p>
          <p><strong>Global livestream count:</strong> <code>{{stats?.TotalCount.ToString() ?? "unknown"}}</code></p>
        </div>

        <div class="card">
          <h2>Update channel metadata</h2>
          <form method="post" action="/channel/update">
            <label>Stream title</label>
            <input name="streamTitle" placeholder="Grinding ranked" />
            <label>Category ID</label>
            <input name="categoryId" placeholder="101" />
            <label>Custom tags (comma-separated)</label>
            <input name="customTags" placeholder="competitive,english,dotnet" />
            <button type="submit">Update Channel</button>
          </form>
        </div>

        <div class="card">
          <h2>Current channel data</h2>
          <table>
            <thead>
              <tr><th>User Id</th><th>Slug</th><th>Title</th><th>Category</th><th>Viewers</th><th>Language</th></tr>
            </thead>
            <tbody>
              {{channelRows}}
            </tbody>
          </table>
        </div>

        <div class="card">
          <h2>Livestream snapshot</h2>
          <p>Use <a href="/discover?language={{WebUtility.UrlEncode(options.DiscoverLanguage)}}&limit=10">/discover</a> to browse more streams.</p>
          <table>
            <thead>
              <tr><th>Slug</th><th>Title</th><th>Category</th><th>Viewers</th><th>Language</th></tr>
            </thead>
            <tbody>
              {{livestreamRows}}
            </tbody>
          </table>
        </div>
      </body>
    </html>
    """;
}

static string RenderSetupPage()
{
    const string configJson = """
{
  "Kick": {
    "ClientId": "your-kick-client-id",
    "ClientSecret": "your-kick-client-secret",
    "BroadcasterUserId": 123456,
    "ChannelSlug": "your-channel-slug",
    "DiscoverLanguage": "en"
  }
}
""";

    return $$"""
    <html>
      <head>
        <title>Kick.NET Channel Dashboard Sample</title>
        <style>
          body { font-family: system-ui, sans-serif; max-width: 900px; margin: 2rem auto; line-height: 1.5; }
          code, pre { background: #f5f5f5; padding: 0.15rem 0.35rem; border-radius: 4px; }
          .card { border: 1px solid #ddd; border-radius: 10px; padding: 1rem 1.25rem; }
        </style>
      </head>
      <body>
        <div class="card">
          <h1>Channel Dashboard Sample Setup</h1>
          <p>Set the following configuration before using this sample:</p>
          <pre>{{WebUtility.HtmlEncode(configJson)}}</pre>
          <p>You can place it in <code>appsettings.Development.json</code> or use environment variables such as <code>Kick__ClientId</code> and <code>Kick__ClientSecret</code>.</p>
        </div>
      </body>
    </html>
    """;
}

static string RenderDiscoverPage(IReadOnlyList<Livestream> livestreams, string highlightedSlug)
{
    var rows = livestreams.Count == 0
        ? "<tr><td colspan=\"5\">No livestreams returned.</td></tr>"
        : string.Join(Environment.NewLine, livestreams.Select(stream =>
        {
            var style = stream.Slug == highlightedSlug ? " style=\"background:#ecfeff;\"" : string.Empty;
            return $$"""
                <tr{{style}}>
                  <td>{{WebUtility.HtmlEncode(stream.Slug ?? "")}}</td>
                  <td>{{WebUtility.HtmlEncode(stream.StreamTitle ?? "")}}</td>
                  <td>{{WebUtility.HtmlEncode(stream.Category?.Name ?? "")}}</td>
                  <td>{{stream.ViewerCount?.ToString() ?? ""}}</td>
                  <td>{{WebUtility.HtmlEncode(stream.Language ?? "")}}</td>
                </tr>
                """;
        }));

    return $$"""
    <html>
      <head>
        <title>Kick.NET Discover Streams</title>
        <style>
          body { font-family: system-ui, sans-serif; max-width: 1100px; margin: 2rem auto; }
          table { border-collapse: collapse; width: 100%; }
          th, td { border-bottom: 1px solid #e7e7e7; text-align: left; padding: 0.6rem; }
        </style>
      </head>
      <body>
        <h1>Discover Streams</h1>
        <p><a href="/">Back to dashboard</a></p>
        <table>
          <thead>
            <tr><th>Slug</th><th>Title</th><th>Category</th><th>Viewers</th><th>Language</th></tr>
          </thead>
          <tbody>
            {{rows}}
          </tbody>
        </table>
      </body>
    </html>
    """;
}

static bool TryCreateClient(ChannelDashboardOptions options, out KickClient kick)
{
    if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
    {
        kick = null!;
        return false;
    }

    kick = KickSdk.CreateAppSession(new KickAppCredentials
    {
        ClientId = options.ClientId,
        ClientSecret = options.ClientSecret,
    }).Client;
    return true;
}

sealed class ChannelDashboardOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public int BroadcasterUserId { get; init; }
    public string? ChannelSlug { get; init; }
    public string? DiscoverLanguage { get; init; }
}
