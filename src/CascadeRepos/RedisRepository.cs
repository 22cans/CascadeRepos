using System.Diagnostics.CodeAnalysis;
using CascadeRepos.Extensions;
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
/// <typeparam name="K">The type of keys used to access the items.</typeparam>
public interface IRedisRepository<T, K> : ICascadeRepository<T, K>, IRedisRepository
{
}

/// <summary>
///     Represents a repository that uses Redis as a data source.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
/// <typeparam name="K">The type of the cache key.</typeparam>
public class RedisRepository<T, K> : CascadeRepository<T, K>, IRedisRepository<T, K>
{
    private readonly IDatabase _database;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisRepository{T, K}" /> class
    ///     using the specified <see cref="IConnectionMultiplexer" /> and <see cref="RedisRepositoryOptions" />.
    /// </summary>
    /// <param name="dateTimeProvider">The provider for retrieving the current date and time in UTC.</param>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer instance.</param>
    /// <param name="options">The options specifying the TTL for the cache items.</param>
    public RedisRepository(IDateTimeProvider dateTimeProvider, IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisRepositoryOptions>? options) : base(dateTimeProvider, options?.Value)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    /// <inheritdoc />
    protected override async Task<T?> CoreGet(K key, CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(KeyToKeyAdapter(key)?.ToString());

        return value.IsNull ? default : JsonConvert.DeserializeObject<T>(value);
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(GetSetAllKey);

        return value.IsNull ? Array.Empty<T>() : JsonConvert.DeserializeObject<IList<T>>(value) ?? Array.Empty<T>();
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetList<L>(L listId, CancellationToken cancellationToken = default)
    {
        var key = GetListKey(listId);
        var value = await _database.StringGetAsync(key);

        return value.IsNull ? Array.Empty<T>() : JsonConvert.DeserializeObject<IList<T>>(value) ?? Array.Empty<T>();
    }

    /// <inheritdoc />
    protected override async Task CoreSet(K key, T item, CancellationToken cancellationToken = default)
    {
        var k = KeyToKeyAdapter(key)?.ToString();
        var expirationTime = CalculateExpirationTime();

        await _database.StringSetAsync(k, JsonConvert.SerializeObject(item));

        if (expirationTime != null)
            await _database.KeyExpireAsync(k!, expirationTime.Value.DateTime);
    }

    /// <inheritdoc />
    protected override async Task CoreSetAll(IList<T> items, CancellationToken cancellationToken = default)
    {
        var expirationTime = CalculateExpirationTime();

        await _database.StringSetAsync(GetSetAllKey, JsonConvert.SerializeObject(items));

        if (expirationTime != null)
            await _database.KeyExpireAsync(GetSetAllKey, expirationTime.Value.DateTime);
    }

    /// <inheritdoc />
    protected override async Task CoreSetList<L>(L listId, IList<T> items,
        CancellationToken cancellationToken = default)
    {
        var expirationTime = CalculateExpirationTime();
        var key = GetListKey(listId);

        await _database.StringSetAsync(key, JsonConvert.SerializeObject(items));

        if (expirationTime != null)
            await _database.KeyExpireAsync(key, expirationTime.Value.DateTime);
    }

    /// <inheritdoc />
    protected override async Task CoreDelete(K key, CancellationToken cancellationToken = default)
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