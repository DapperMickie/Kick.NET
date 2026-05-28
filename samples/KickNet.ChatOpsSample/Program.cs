using Kick;
using Spectre.Console;

if (args.Length > 0 && args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
{
    AnsiConsole.MarkupLine("[grey]Environment:[/] KICK_CLIENT_ID, KICK_CLIENT_SECRET, KICK_BROADCASTER_USER_ID");
    AnsiConsole.MarkupLine("[grey]Commands:[/] send, reply, delete, ban, unban, quit");
    AnsiConsole.MarkupLine("[grey]Note:[/] If KICK rejects chat or moderation for missing user scopes, use the user-auth sample path instead.");
    return;
}

if (!int.TryParse(Environment.GetEnvironmentVariable("KICK_BROADCASTER_USER_ID"), out var broadcasterId))
{
    AnsiConsole.MarkupLine("[red]Set KICK_BROADCASTER_USER_ID before running this sample.[/]");
    return;
}

var clientId = Environment.GetEnvironmentVariable("KICK_CLIENT_ID");
var clientSecret = Environment.GetEnvironmentVariable("KICK_CLIENT_SECRET");
if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
{
    AnsiConsole.MarkupLine("[red]Set KICK_CLIENT_ID and KICK_CLIENT_SECRET before running this sample.[/]");
    return;
}

var kick = KickSdk.CreateAppSession(new KickAppCredentials
{
    ClientId = clientId,
    ClientSecret = clientSecret,
}).Client;

AnsiConsole.Write(new FigletText("Chat Ops").Color(Color.Cyan1));
AnsiConsole.MarkupLine("[grey]Commands:[/] send, reply, delete, ban, unban, quit");
AnsiConsole.MarkupLine("[grey]Auth:[/] default app-auth session; some commands may require a user-authenticated session in practice.");

while (true)
{
    var raw = AnsiConsole.Ask<string>("[green]chat-ops>[/]");
    if (string.IsNullOrWhiteSpace(raw))
    {
        continue;
    }

    var parts = raw.Split(' ', 2, StringSplitOptions.TrimEntries);
    var command = parts[0].ToLowerInvariant();
    var payload = parts.Length > 1 ? parts[1] : string.Empty;

    switch (command)
    {
        case "send":
            await SendAsync(payload);
            break;
        case "reply":
            await ReplyAsync(payload);
            break;
        case "delete":
            await DeleteAsync(payload);
            break;
        case "ban":
            await BanAsync(payload);
            break;
        case "unban":
            await UnbanAsync(payload);
            break;
        case "quit":
        case "exit":
            return;
        default:
            AnsiConsole.MarkupLine("[red]Unknown command.[/]");
            break;
    }
}

async Task SendAsync(string payload)
{
    if (string.IsNullOrWhiteSpace(payload))
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] send <message>");
        return;
    }

    var request = new PostChatMessageRequestBuilder()
        .ForBroadcaster(broadcasterId)
        .WithContent(payload)
        .AsBot()
        .Build();

    var response = await kick.Chat.PostMessageAsync(request);
    RenderResult("Message sent", response?.Data);
}

async Task ReplyAsync(string payload)
{
    var split = payload.Split(' ', 2, StringSplitOptions.TrimEntries);
    if (split.Length < 2)
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] reply <message-id> <content>");
        return;
    }

    var request = new PostChatMessageRequestBuilder()
        .ForBroadcaster(broadcasterId)
        .WithContent(split[1])
        .ReplyTo(split[0])
        .AsBot()
        .Build();

    var response = await kick.Chat.PostMessageAsync(request);
    RenderResult("Reply sent", response?.Data);
}

async Task DeleteAsync(string payload)
{
    if (string.IsNullOrWhiteSpace(payload))
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] delete <message-id>");
        return;
    }

    await kick.Chat.DeleteMessageAsync(payload);
    AnsiConsole.MarkupLine("[yellow]Message deleted.[/]");
}

async Task BanAsync(string payload)
{
    if (!int.TryParse(payload, out var userId))
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] ban <user-id>");
        return;
    }

    var reason = AnsiConsole.Ask<string>("Reason:");
    var duration = AnsiConsole.Prompt(new TextPrompt<int>("Timeout minutes:").DefaultValue(10));

    var request = new BanUserRequestBuilder()
        .ForBroadcaster(broadcasterId)
        .ForUser(userId)
        .WithReason(reason)
        .TimeoutForMinutes(duration)
        .Build();

    var response = await kick.Moderation.BanAsync(request);
    RenderResult("User banned", response?.Data);
}

async Task UnbanAsync(string payload)
{
    if (!int.TryParse(payload, out var userId))
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] unban <user-id>");
        return;
    }

    var request = new UnbanUserRequestBuilder()
        .ForBroadcaster(broadcasterId)
        .ForUser(userId)
        .Build();

    var response = await kick.Moderation.UnbanAsync(request);
    RenderResult("User unbanned", response?.Data);
}

static void RenderResult<T>(string title, T value)
{
    var panel = new Panel(new Text(System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
    })))
    {
        Header = new PanelHeader(title),
    };

    AnsiConsole.Write(panel);
}
