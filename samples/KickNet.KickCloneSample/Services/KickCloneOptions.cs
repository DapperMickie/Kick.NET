namespace KickNet.KickCloneSample.Services;

public sealed class KickCloneOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string? RedirectUri { get; init; }
    public string DiscoverLanguage { get; init; } = "en";
    public int DiscoverLimit { get; init; } = 12;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}
