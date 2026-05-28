using Kick;
using Spectre.Console;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
var commands = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase)
{
    ["help"] = ShowHelpAsync,
    ["list"] = ListRewardsAsync,
    ["create"] = CreateRewardAsync,
    ["update"] = UpdateRewardAsync,
    ["redemptions"] = ListRedemptionsAsync,
    ["accept"] = AcceptRedemptionsAsync,
    ["reject"] = RejectRedemptionsAsync,
    ["demo"] = DemoAsync,
};

if (!commands.TryGetValue(command, out var action))
{
    AnsiConsole.MarkupLine($"[red]Unknown command:[/] {Markup.Escape(command)}");
    await ShowHelpAsync();
    return 1;
}

await action();
return 0;

Task ShowHelpAsync()
{
    var table = new Table().RoundedBorder();
    table.AddColumn("Command");
    table.AddColumn("Use Case");
    table.AddRow("list", "List current channel rewards");
    table.AddRow("create", "Create a reward using the builder API");
    table.AddRow("update", "Update an existing reward");
    table.AddRow("redemptions", "View pending reward redemptions");
    table.AddRow("accept", "Accept redemptions by id");
    table.AddRow("reject", "Reject redemptions by id");
    table.AddRow("demo", "Run a non-destructive showcase of the request shapes");
    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[grey]Environment:[/] KICK_CLIENT_ID, KICK_CLIENT_SECRET, KICK_REWARD_ID, KICK_REDEMPTION_IDS");
    return Task.CompletedTask;
}

async Task DemoAsync()
{
    var create = new CreateChannelRewardRequestBuilder()
        .WithTitle("Song Request")
        .WithCost(250)
        .WithDescription("Paste a valid track URL")
        .WithBackgroundColor("#00e701")
        .RequiresUserInput()
        .IsEnabled()
        .Build();

    var update = new UpdateChannelRewardRequestBuilder()
        .WithTitle("Priority Song Request")
        .WithCost(500)
        .IsEnabled()
        .IsPaused(false)
        .Build();

    var accept = BuildBulkIds();

    AnsiConsole.Write(new Panel("Create reward payload").Header("Builder Output"));
    DumpJson(create);
    AnsiConsole.Write(new Panel("Update reward payload").Header("Builder Output"));
    DumpJson(update);
    AnsiConsole.Write(new Panel("Bulk redemption action payload").Header("Builder Output"));
    DumpJson(accept);

    await Task.CompletedTask;
}

async Task ListRewardsAsync()
{
    var kick = RequireClient();
    var response = await kick.ChannelRewards.GetAsync();
    var table = new Table().RoundedBorder();
    table.AddColumn("Id");
    table.AddColumn("Title");
    table.AddColumn("Cost");
    table.AddColumn("Enabled");
    table.AddColumn("Paused");

    foreach (var reward in response?.Data ?? [])
    {
        table.AddRow(
            reward.Id ?? string.Empty,
            reward.Title ?? string.Empty,
            reward.Cost?.ToString() ?? string.Empty,
            reward.IsEnabled?.ToString() ?? string.Empty,
            reward.IsPaused?.ToString() ?? string.Empty);
    }

    AnsiConsole.Write(table);
}

async Task CreateRewardAsync()
{
    var kick = RequireClient();
    var title = Ask("Reward title", "Song Request");
    var cost = AskInt("Reward cost", 250);
    var description = Ask("Reward description", "Paste a valid track URL");
    var background = Ask("Background color", "#00e701");
    var requiresInput = AskYesNo("Require user input?", true);
    var enabled = AskYesNo("Enable reward immediately?", true);

    var request = new CreateChannelRewardRequestBuilder()
        .WithTitle(title)
        .WithCost(cost)
        .WithDescription(description)
        .WithBackgroundColor(background)
        .RequiresUserInput(requiresInput)
        .IsEnabled(enabled)
        .Build();

    var response = await kick.ChannelRewards.CreateAsync(request);
    AnsiConsole.MarkupLine("[green]Reward created.[/]");
    DumpJson(response?.Data);
}

async Task UpdateRewardAsync()
{
    var kick = RequireClient();
    var rewardId = Require("KICK_REWARD_ID");
    var title = Ask("New title", "Priority Song Request");
    var cost = AskInt("New cost", 500);
    var enabled = AskYesNo("Enabled?", true);
    var paused = AskYesNo("Paused?", false);

    var request = new UpdateChannelRewardRequestBuilder()
        .WithTitle(title)
        .WithCost(cost)
        .IsEnabled(enabled)
        .IsPaused(paused)
        .Build();

    var response = await kick.ChannelRewards.UpdateAsync(rewardId, request);
    AnsiConsole.MarkupLine("[green]Reward updated.[/]");
    DumpJson(response?.Data);
}

async Task ListRedemptionsAsync()
{
    var kick = RequireClient();
    var rewardId = Environment.GetEnvironmentVariable("KICK_REWARD_ID");
    var response = await kick.ChannelRewards.GetRedemptionsAsync(new GetRewardRedemptionsRequest
    {
        RewardId = rewardId,
        Status = "pending",
    });

    foreach (var reward in response?.Data ?? [])
    {
        var table = new Table().RoundedBorder();
        table.Title = new TableTitle(reward.Reward?.Title ?? reward.Reward?.Id ?? "Reward");
        table.AddColumn("Redemption Id");
        table.AddColumn("Status");
        table.AddColumn("Redeemer");
        table.AddColumn("Input");

        foreach (var redemption in reward.Redemptions ?? [])
        {
            table.AddRow(
                redemption.Id ?? string.Empty,
                redemption.Status ?? string.Empty,
                redemption.Redeemer?.UserId.ToString() ?? string.Empty,
                redemption.UserInput ?? string.Empty);
        }

        AnsiConsole.Write(table);
    }
}

async Task AcceptRedemptionsAsync()
{
    var kick = RequireClient();
    var request = BuildBulkIds();
    var response = await kick.ChannelRewards.AcceptRedemptionsAsync(request);
    AnsiConsole.MarkupLine("[green]Accept request submitted.[/]");
    DumpJson(response?.Data);
}

async Task RejectRedemptionsAsync()
{
    var kick = RequireClient();
    var request = BuildBulkIds();
    var response = await kick.ChannelRewards.RejectRedemptionsAsync(request);
    AnsiConsole.MarkupLine("[yellow]Reject request submitted.[/]");
    DumpJson(response?.Data);
}

KickClient RequireClient()
{
    var session = KickSdk.CreateAppSession(new KickAppCredentials
    {
        ClientId = Require("KICK_CLIENT_ID"),
        ClientSecret = Require("KICK_CLIENT_SECRET"),
    });
    return session.Client;
}

BulkRedemptionActionRequest BuildBulkIds()
{
    var ids = (Environment.GetEnvironmentVariable("KICK_REDEMPTION_IDS") ?? "01KEWZ2RKYZZFGD154K50Y3F8D,01KEWZ2YC27070XWMHKFJR6C87")
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    var builder = new BulkRedemptionActionRequestBuilder();
    foreach (var id in ids)
    {
        builder.AddId(id);
    }

    return builder.Build();
}

static string Require(string name)
{
    return Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Missing environment variable '{name}'.");
}

static string Ask(string prompt, string defaultValue)
{
    return AnsiConsole.Prompt(new TextPrompt<string>($"{prompt}:").DefaultValue(defaultValue));
}

static int AskInt(string prompt, int defaultValue)
{
    return AnsiConsole.Prompt(new TextPrompt<int>($"{prompt}:").DefaultValue(defaultValue));
}

static bool AskYesNo(string prompt, bool defaultValue)
{
    return AnsiConsole.Confirm(prompt, defaultValue);
}

static void DumpJson<T>(T value)
{
    AnsiConsole.Write(new Text(System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
    })));
    AnsiConsole.WriteLine();
}
