using System.Diagnostics.CodeAnalysis;
using CascadeRepos.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CascadeRepos;

/// <summary>
///     Represents an interface for a MemoryCache repository.
/// </summary>
public interface IMemoryCacheRepository
{
}

/// <summary>
///     Represents an interface for a MemoryCache repository.
/// </summary>
/// <typeparam name="T">The type of items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of keys used to access the items.</typeparam>
public interface IMemoryCacheRepository<T, TK> : ICascadeRepository<T, TK>, IMemoryCacheRepository
{
}

/// <summary>
///     Represents a repository that uses the memory cache as a data source.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
/// <typeparam name="TK">The type of the cache key.</typeparam>
public class MemoryCacheRepository<T, TK> : CascadeRepository<T, TK>, IMemoryCacheRepository<T, TK>
{
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MemoryCacheRepository{T, K}" /> class
    ///     using the specified <see cref="IMemoryCache" /> and <see cref="MemoryCacheRepositoryOptions" />.
    /// </summary>
    /// <param name="logger">The logger instance used for logging.</param>
    /// <param name="dateTimeProvider">The provider for retrieving the current date and time in UTC.</param>
    /// <param name="memoryCache">The memory cache instance to be used.</param>
    /// <param name="options">The options specifying the TTL for the cache items.</param>
    public MemoryCacheRepository(ILogger<CascadeRepository<T, TK>> logger, IDateTimeProvider dateTimeProvider,
        IMemoryCache memoryCache, IOptions<MemoryCacheRepositoryOptions>? options) : base(logger, dateTimeProvider,
        options?.Value)
    {
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    protected override async Task<T?> CoreGet(TK key, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_memoryCache.Get<T?>(KeyToKeyAdapter(key)!));
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_memoryCache.Get<IList<T>>(GetSetAllKey) ?? new List<T>());
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetList<TL>(TL listId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_memoryCache.Get<IList<T>>(GetListKey(listId)) ?? new List<T>());
    }

    /// <inheritdoc />
    protected override Task CoreSet(TK key, T item, CancellationToken cancellationToken = default)
    {
        var expirationTime = CalculateExpirationTime();
        var cacheEntryOptions = new MemoryCacheEntryOptions();

        switch (ExpirationType)
        {
            case ExpirationType.Sliding when TimeToLive != null:
                cacheEntryOptions.SetSlidingExpiration(TimeToLive.Value);
                break;
            case ExpirationType.Absolute when expirationTime != null:
                cacheEntryOptions.SetAbsoluteExpiration(expirationTime.Value);
                break;
        }

        _memoryCache.Set(KeyToKeyAdapter(key)!, item, cacheEntryOptions);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task CoreSetAll(IList<T> items, CancellationToken cancellationToken = default)
    {
        var expirationTime = CalculateExpirationTime();
        var key = GetSetAllKey;

        if (expirationTime == null)
            _memoryCache.Set(key, items);
        else
            _memoryCache.Set(key, items, expirationTime.Value);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task CoreSetList<TL>(TL listId, IList<T> items, CancellationToken cancellationToken = default)
    {
        var expirationTime = CalculateExpirationTime();
        var key = GetListKey(listId);

        if (expirationTime == null)
            _memoryCache.Set(key, items);
        else
            _memoryCache.Set(key, items, expirationTime.Value);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task CoreDelete(TK key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(KeyToKeyAdapter(key)!);
        await Task.CompletedTask;
    }
}

/// <summary>
///     Represents the options for the <see cref="MemoryCacheRepository{T, K}" /> class.
/// </summary>
[ExcludeFromCodeCoverage]
public class MemoryCacheRepositoryOptions : CascadeRepositoryOptions
{
    /// <summary>
    ///     The configuration path for the memory cache repository options.
    /// </summary>
    public const string ConfigPath = "CascadeRepos:MemoryCache";
}