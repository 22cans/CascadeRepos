using System.Diagnostics.CodeAnalysis;
using CascadeRepos.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace CascadeRepos;

/// <summary>
///     Represents an interface for a Redis repository.
/// </summary>
public interface IRedisRepository
{
}

/// <summary>
///     Represents an interface for a Redis repository.
/// </summary>
/// <typeparam name="T">The type of items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of keys used to access the items.</typeparam>
public interface IRedisRepository<T, TK> : ICascadeRepository<T, TK>, IRedisRepository
{
}

/// <summary>
///     Represents a repository that uses Redis as a data source.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
/// <typeparam name="TK">The type of the cache key.</typeparam>
public class RedisRepository<T, TK> : CascadeRepository<T, TK>, IRedisRepository<T, TK>
{
    private readonly IDatabase _database;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisRepository{T, K}" /> class
    ///     using the specified <see cref="IConnectionMultiplexer" /> and <see cref="RedisRepositoryOptions" />.
    /// </summary>
    /// <param name="logger">The logger instance used for logging.</param>
    /// <param name="dateTimeProvider">The provider for retrieving the current date and time in UTC.</param>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer instance.</param>
    /// <param name="options">The options specifying the TTL for the cache items.</param>
    public RedisRepository(ILogger<CascadeRepository<T, TK>> logger, IDateTimeProvider dateTimeProvider,
        IConnectionMultiplexer connectionMultiplexer, IOptions<RedisRepositoryOptions>? options) : base(logger,
        dateTimeProvider, options?.Value)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    /// <summary>
    ///     Calculates the expiration time for a cache item based on the configured time-to-live and absolute expiration time.
    /// </summary>
    /// <returns>The calculated expiration time, or <c>null</c> if no expiration time is set.</returns>
    protected internal new TimeSpan? CalculateExpirationTime()
    {
        var expiry = base.CalculateExpirationTime();
        if (!expiry.HasValue)
            return null;

        return expiry - DateTimeProvider.GetUtcNow();
    }

    /// <inheritdoc />
    protected override async Task<T?> CoreGet(TK key, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(KeyToKeyAdapter(key)?.ToString());

        return value.IsNull ? default : JsonConvert.DeserializeObject<T>(value);
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(GetSetAllKey);

        return value.IsNull ? new List<T>() : JsonConvert.DeserializeObject<IList<T>>(value) ?? new List<T>();
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetList<TL>(TL listId, CancellationToken cancellationToken = default)
    {
        var key = GetListKey(listId);
        var value = await _database.StringGetAsync(key);

        return value.IsNull ? new List<T>() : JsonConvert.DeserializeObject<IList<T>>(value) ?? new List<T>();
    }

    /// <inheritdoc />
    protected override async Task CoreSet(TK key, T item, CancellationToken cancellationToken = default)
    {
        var k = KeyToKeyAdapter(key)?.ToString();
        var expirationTime = CalculateExpirationTime();

        await _database.StringSetAsync(k, JsonConvert.SerializeObject(item));

        if (expirationTime != null)
            await _database.KeyExpireAsync(k!, expirationTime.Value);
    }

    /// <inheritdoc />
    protected override async Task CoreSetAll(IList<T> items, CancellationToken cancellationToken = default)
    {
        var expirationTime = CalculateExpirationTime();

        await _database.StringSetAsync(GetSetAllKey, JsonConvert.SerializeObject(items));

        if (expirationTime != null)
            await _database.KeyExpireAsync(GetSetAllKey, expirationTime.Value);
    }

    /// <inheritdoc />
    protected override async Task CoreSetList<TL>(TL listId, IList<T> items,
        CancellationToken cancellationToken = default)
    {
        var expirationTime = CalculateExpirationTime();
        var key = GetListKey(listId);

        await _database.StringSetAsync(key, JsonConvert.SerializeObject(items));

        if (expirationTime != null)
            await _database.KeyExpireAsync(key, expirationTime.Value);
    }

    /// <inheritdoc />
    protected override async Task CoreDelete(TK key, CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(key!.ToString());
    }
}

/// <summary>
///     Represents the options for the <see cref="RedisRepository{T, K}" /> class.
/// </summary>
[ExcludeFromCodeCoverage]
public class RedisRepositoryOptions : CascadeRepositoryOptions
{
    /// <summary>
    ///     The configuration path for the Redis repository options.
    /// </summary>
    public const string ConfigPath = "CascadeRepos:Redis";
}