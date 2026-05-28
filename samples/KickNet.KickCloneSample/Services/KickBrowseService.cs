using Kick;
using Microsoft.Extensions.Options;

namespace KickNet.KickCloneSample.Services;

public sealed class KickBrowseService(IOptions<KickCloneOptions> options)
{
    private readonly KickCloneOptions _options = options.Value;
    private readonly Lazy<KickAppSession> _appSession = new(() => KickSdk.CreateAppSession(new KickAppCredentials
    {
        ClientId = options.Value.ClientId,
        ClientSecret = options.Value.ClientSecret,
    }));

    public bool IsConfigured => _options.IsConfigured;
    public KickCloneOptions Options => _options;

    public async Task<BrowseSnapshot> GetSnapshotAsync(string? slug, string? categoryId, string? language, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return new BrowseSnapshot([], null, null);
        }

        var category = int.TryParse(categoryId, out var parsedCategoryId) ? parsedCategoryId : (int?)null;
        var streams = await _appSession.Value.Client.Livestreams.GetAsync(new GetLivestreamsRequest
        {
            CategoryId = category,
            Language = string.IsNullOrWhiteSpace(language) ? _options.DiscoverLanguage : language,
            Limit = _options.DiscoverLimit,
            Sort = "viewer_count",
        }, cancellationToken);

        Channel? selected = null;
        if (!string.IsNullOrWhiteSpace(slug))
        {
            selected = (await _appSession.Value.Client.Channels.GetAsync(new GetChannelsRequest
            {
                Slugs = [slug],
            }, cancellationToken))?.Data?.FirstOrDefault();
        }

        var stats = await _appSession.Value.Client.Livestreams.GetStatsAsync(cancellationToken);
        return new BrowseSnapshot(streams?.Data ?? [], selected, stats?.Data);
    }
}

public sealed record BrowseSnapshot(
    IReadOnlyList<Livestream> Streams,
    Channel? SelectedChannel,
    LivestreamStats? Stats);
