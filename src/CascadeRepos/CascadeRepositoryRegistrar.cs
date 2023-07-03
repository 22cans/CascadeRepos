using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CascadeRepos;

/// <summary>
///     Provides methods to register and configure cascade repositories.
/// </summary>
public static class CascadeRepositoryRegistrar
{
    /// <summary>
    ///     Configures the cascade of repositories using the specified configuration.
    /// </summary>
    /// <param name="services">The service collection to configure the cascade repositories.</param>
    /// <param name="configuration">The configuration to retrieve repository settings from.</param>
    /// <returns>The modified service collection.</returns>
    /// <remarks>
    ///     This method configures the cascade of repositories using the specified configuration.
    ///     It retrieves the repository settings from the <paramref name="configuration" /> and registers the necessary
    ///     repository types in the service collection.
    ///     Example usage:
    ///     <code>
    /// // To set up CascadeRepos
    /// services.ConfigureCascadeRepos(configuration);
    /// </code>
    /// </remarks>
    public static IServiceCollection ConfigureCascadeRepos(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddMemoryCacheRepository(configuration)
            .AddRedisRepository(configuration)
            .AddRedisHashRepository()
            .AddDynamoDbRepository()
            .AddGenericRepository();
    }

    private static IServiceCollection AddMemoryCacheRepository(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MemoryCacheRepositoryOptions>(
            options => configuration
                .GetSection(MemoryCacheRepositoryOptions.ConfigPath)
                .Bind(options));

        return services
            .AddTransient(typeof(MemoryCacheRepository<,>))
            .AddTransient(typeof(IMemoryCacheRepository<,>), typeof(MemoryCacheRepository<,>));
    }

    private static IServiceCollection AddRedisHashRepository(this IServiceCollection services)
    {
        return services
            .AddTransient(typeof(RedisHashRepository<,>))
            .AddTransient(typeof(IRedisHashRepository<,>), typeof(RedisHashRepository<,>));
    }

    private static IServiceCollection AddRedisRepository(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisRepositoryOptions>(
            options => configuration
                .GetSection(RedisRepositoryOptions.ConfigPath)
                .Bind(options));

        return services
            .AddTransient(typeof(RedisRepository<,>))
            .AddTransient(typeof(IRedisRepository<,>), typeof(RedisRepository<,>));
    }

    private static IServiceCollection AddDynamoDbRepository(this IServiceCollection services)
    {
        return services
            .AddTransient(typeof(DynamoDbRepository<,>));
    }

    private static IServiceCollection AddGenericRepository(this IServiceCollection services)
    {
        return services
            .AddTransient(typeof(GenericRepository<,>));
    }

    /// <summary>
    ///     Configures a custom type into the Dependency Injection.
    /// </summary>
    /// <param name="services">The service collection to configure the cascade repositories.</param>
    /// <typeparam name="T">The type interface to be registered</typeparam>
    /// <typeparam name="C">The type concrete class to be registered</typeparam>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection ConfigureCustomType<T, C>(this IServiceCollection services)
        where T : class where C : class, T
    {
        return services.AddTransient<T, C>();
    }

    /// <summary>
    ///     Adds a cascade of repositories for the specified types <typeparamref name="T" /> and <typeparamref name="K" /> to
    ///     the service collection.
    /// </summary>
    /// <typeparam name="T">The type of the items stored in the repositories.</typeparam>
    /// <typeparam name="K">The type of the keys used to access the items.</typeparam>
    /// <param name="services">The service collection to add the repositories to.</param>
    /// <param name="repoTypes">An array of repository types to be included in the cascade.</param>
    /// <returns>The modified service collection.</returns>
    /// <remarks>
    ///     This method creates a cascade of repositories for the specified types <typeparamref name="T" /> and
    ///     <typeparamref name="K" /> and adds them to the service collection.
    ///     The repositories are instantiated based on the provided repository types <paramref name="repoTypes" /> and
    ///     connected in the order specified.
    ///     Example usage:
    ///     <code>
    /// // To set up a custom cascade repository for Foo class with Bar as key type
    /// services.AddCascadeRepos&lt;Foo, Bar&gt;(
    ///     typeof(MemoryCacheRepository&lt;,&gt;),
    ///     typeof(RedisRepository&lt;,&gt;),
    ///     typeof(DynamoDBRepository&lt;,&gt;));
    /// </code>
    /// </remarks>
    public static IServiceCollection AddCascadeRepos<T, K>(this IServiceCollection services, params Type[] repoTypes)
    {
        return services
            .AddTransient(serviceProvider =>
            {
                ICascadeRepository<T, K>? firstRepo = null, lastRepo = null;

                foreach (var repoType in repoTypes)
                {
                    var constructedType = repoType.IsGenericType
                        ? repoType.MakeGenericType(typeof(T), typeof(K))
                        : repoType;
                    var repo = (ICascadeRepository<T, K>)serviceProvider.GetRequiredService(constructedType);

                    lastRepo?.SetNext(repo);
                    firstRepo ??= repo;
                    lastRepo = repo;
                }

                return firstRepo!;
            });
    }
}