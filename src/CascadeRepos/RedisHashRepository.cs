using CascadeRepos.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace CascadeRepos;

/// <summary>
///     Represents an interface for a Redis Hash repository.
/// </summary>
public interface IRedisHashRepository
{
}

/// <summary>
///     Represents an interface for a Redis Hash repository.
/// </summary>
/// <typeparam name="T">The type of items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of keys used to access the items.</typeparam>
public interface IRedisHashRepository<T, TK> : ICascadeRepository<T, TK>, IRedisHashRepository
{
    /// <summary>
    ///     Sets the Redis key adapter function used to adapt keys before accessing the Redis hash repository.
    /// </summary>
    /// <param name="redisKeyAdapter">The Redis key adapter function.</param>
    /// <returns>The modified Redis hash repository.</returns>
    IRedisHashRepository<T, TK> AdaptRedisKey(Func<TK> redisKeyAdapter);
}

/// <summary>
///     Represents a repository implementation using Redis Hash.
/// </summary>
/// <typeparam name="T">The type of the items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of the keys used to access the items.</typeparam>
public class RedisHashRepository<T, TK> : CascadeRepository<T, TK>, IRedisHashRepository<T, TK>
{
    private readonly IDatabase _database;
    private Func<TK> _redisKeyAdapter = () => (TK)(object)typeof(T).Name;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisHashRepository{T, K}" /> class.
    /// </summary>
    /// <param name="logger">The logger instance used for logging.</param>
    /// <param name="dateTimeProvider">The provider for retrieving the current date and time in UTC.</param>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer.</param>
    public RedisHashRepository(ILogger<CascadeRepository<T, TK>> logger, IDateTimeProvider dateTimeProvider,
        IConnectionMultiplexer connectionMultiplexer) : base(logger, dateTimeProvider, null)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The key adapter will be used as field by RedisHash. Check usage on <see cref="CoreSetAll" />.
    /// </remarks>
    public override ICascadeRepository<T, TK> AdaptObjectToKey(Func<T, TK> keyAdapter)
    {
        return base.AdaptObjectToKey(keyAdapter);
    }

    /// <inheritdoc />
    public IRedisHashRepository<T, TK> AdaptRedisKey(Func<TK> redisKeyAdapter)
    {
        _redisKeyAdapter = redisKeyAdapter;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<T?> CoreGet(TK key, CancellationToken cancellationToken = default)
    {
        var value = await _database.HashGetAsync(_redisKeyAdapter()!.ToString(), KeyToKeyAdapter(key)?.ToString());

        return value.IsNull ? default : JsonConvert.DeserializeObject<T>(value);
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default)
    {
        var result = await _database.HashGetAllAsync(GetSetAllKey);
        return result is null
            ? Array.Empty<T>()
            : result.Select(x => JsonConvert.DeserializeObject<T>(x.Value)!).ToList();
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetList<TL>(TL listId, CancellationToken cancellationToken = default)
    {
        var result = await _database.HashGetAllAsync(GetListKey(listId));
        return result is null
            ? Array.Empty<T>()
            : result.Select(x => JsonConvert.DeserializeObject<T>(x.Value)!).ToList();
    }

    /// <inheritdoc />
    protected override async Task CoreSet(TK key, T item, CancellationToken cancellationToken = default)
    {
        await _database.HashSetAsync(_redisKeyAdapter()!.ToString(), KeyToKeyAdapter(key)!.ToString(),
            JsonConvert.SerializeObject(item));

        await SetExpiration();
    }

    /// <inheritdoc />
    protected override async Task CoreSetAll(IList<T> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
            await _database.HashSetAsync(GetSetAllKey, ObjectToKeyAdapter(item)!.ToString(),
                JsonConvert.SerializeObject(item));
        
        await SetExpiration();
    }

    /// <inheritdoc />
    protected override async Task CoreSetList<TL>(TL listId, IList<T> items,
        CancellationToken cancellationToken = default)
    {
        var key = GetListKey(listId);
        foreach (var item in items)
            await _database.HashSetAsync(key, ObjectToKeyAdapter(item)!.ToString(),
                JsonConvert.SerializeObject(item));
    }

    /// <inheritdoc />
    protected override async Task CoreDelete(TK key, CancellationToken cancellationToken = default)
    {
        await _database.HashDeleteAsync(_redisKeyAdapter()!.ToString(), KeyToKeyAdapter(key)!.ToString());
    }

    private async Task SetExpiration()
    {
        var expirationTime = CalculateExpirationTime();
        
        if (expirationTime != null)
            await _database.KeyExpireAsync(_redisKeyAdapter()!.ToString(), expirationTime.Value.DateTime);
    }
}