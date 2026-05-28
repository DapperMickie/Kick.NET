using Kick;

var channel = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("KICK_CHANNEL_SLUG") ?? "xqc";

var limit = TryReadLimit() ?? 10;
var kick = new KickClient(options: new KickClientOptions
{
    EnableExperimentalWebsiteApi = true,
});

Console.WriteLine("Kick.NET experimental media sample");
Console.WriteLine("These calls use undocumented kick.com website endpoints.");
Console.WriteLine($"Channel: {channel}");
Console.WriteLine($"Limit  : {limit}");
Console.WriteLine();

try
{
    var videos = await kick.Experimental.Videos.GetByChannelAsync(channel);
    PrintVideos("Videos", videos, limit);

    var latestVideos = await kick.Experimental.Videos.GetLatestByChannelAsync(channel);
    var latestVideo = latestVideos?.FirstOrDefault();
    PrintLatestVideo(latestVideo);

    var clips = await kick.Experimental.Clips.GetByChannelAsync(new GetChannelWebsiteClipsRequest
    {
        Channel = channel,
        Limit = limit,
    });
    PrintClips("Clips", clips, limit);
}
catch (KickApiException ex)
{
    Console.Error.WriteLine($"Kick returned HTTP {(int)ex.StatusCode}: {ex.Message}");
    if (!string.IsNullOrWhiteSpace(ex.ResponseBody))
    {
        Console.Error.WriteLine(ex.ResponseBody);
    }

    return 1;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Request failed: {ex.Message}");
    return 1;
}

return 0;

static int? TryReadLimit()
{
    var value = Environment.GetEnvironmentVariable("KICK_MEDIA_LIMIT");
    return int.TryParse(value, out var limit) && limit > 0 ? limit : null;
}

static void PrintLatestVideo(KickWebsiteVideo? video)
{
    Console.WriteLine();
    Console.WriteLine("Latest video");
    Console.WriteLine("------------");

    if (video is null)
    {
        Console.WriteLine("No latest video returned.");
        return;
    }

    Console.WriteLine(video.Title ?? "(untitled)");
    Console.WriteLine($"Id       : {video.Id}");
    Console.WriteLine($"Slug     : {video.Slug ?? "(none)"}");
    Console.WriteLine($"Duration : {FormatDuration(video.Duration)}");
    Console.WriteLine($"Views    : {video.Views?.ToString() ?? "(unknown)"}");
    Console.WriteLine($"Created  : {FormatDate(video.CreatedAt)}");
}

static void PrintVideos(string title, IReadOnlyList<KickWebsiteVideo>? videos, int limit)
{
    Console.WriteLine(title);
    Console.WriteLine(new string('-', title.Length));

    if (videos is null || videos.Count == 0)
    {
        Console.WriteLine("No videos returned.");
        return;
    }

    foreach (var video in videos.Take(limit))
    {
        Console.WriteLine($"- {video.Title ?? "(untitled)"}");
        Console.WriteLine($"  Id: {video.Id} | Duration: {FormatDuration(video.Duration)} | Views: {video.Views?.ToString() ?? "(unknown)"}");
        Console.WriteLine($"  Created: {FormatDate(video.CreatedAt)}");
    }
}

static void PrintClips(string title, IReadOnlyList<KickWebsiteClip>? clips, int limit)
{
    Console.WriteLine();
    Console.WriteLine(title);
    Console.WriteLine(new string('-', title.Length));

    if (clips is null || clips.Count == 0)
    {
        Console.WriteLine("No clips returned.");
        return;
    }

    foreach (var clip in clips.Take(limit))
    {
        Console.WriteLine($"- {clip.Title ?? "(untitled)"}");
        Console.WriteLine($"  Slug: {clip.Slug ?? "(none)"} | Duration: {FormatDuration(clip.Duration)} | Views: {clip.Views?.ToString() ?? "(unknown)"}");
        Console.WriteLine($"  Created: {FormatDate(clip.CreatedAt)}");
    }
}

static string FormatDuration(int? seconds)
{
    if (!seconds.HasValue)
    {
        return "(unknown)";
    }

    var duration = TimeSpan.FromSeconds(seconds.Value);
    return duration.TotalHours >= 1
        ? duration.ToString(@"h\:mm\:ss")
        : duration.ToString(@"m\:ss");
}

static string FormatDate(DateTimeOffset? date)
{
    return date?.ToString("u") ?? "(unknown)";
}
