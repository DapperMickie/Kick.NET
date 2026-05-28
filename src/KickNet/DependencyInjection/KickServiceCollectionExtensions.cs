using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kick.DependencyInjection;

public static class KickServiceCollectionExtensions
{
    public static IServiceCollection AddKickAppSession(this IServiceCollection services, KickAppCredentials credentials, Action<KickAppSessionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(credentials);

        var options = new KickAppSessionOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(credentials);
        services.TryAddSingleton(options);
        services.TryAddSingleton(static sp => new KickAppSession(
            sp.GetRequiredService<KickAppCredentials>(),
            sp.GetRequiredService<KickAppSessionOptions>()));
        services.TryAddSingleton(static sp => sp.GetRequiredService<KickAppSession>().Client);
        return services;
    }

    public static IServiceCollection AddKickUserAuth(this IServiceCollection services, KickUserAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton(static sp => new KickUserAuthClient(sp.GetRequiredService<KickUserAuthOptions>()));
        return services;
    }

    public static IServiceCollection AddKickClient(this IServiceCollection services, Action<KickClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new KickClientOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton(static sp => new KickClient(options: sp.GetRequiredService<KickClientOptions>()));
        return services;
    }

    public static IServiceCollection AddKickClient<TProvider>(this IServiceCollection services, Action<KickClientOptions>? configure = null)
        where TProvider : class, IKickAccessTokenProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new KickClientOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<TProvider>();
        services.TryAddSingleton<IKickAccessTokenProvider>(static sp => sp.GetRequiredService<TProvider>());
        services.TryAddSingleton(static sp => new KickClient(accessTokenProvider: sp.GetRequiredService<IKickAccessTokenProvider>(), options: sp.GetRequiredService<KickClientOptions>()));
        return services;
    }
}
