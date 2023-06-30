using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cascade;

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
/// <typeparam name="K">The type of keys used to access the items.</typeparam>
public interface IMemoryCacheRepository<T, K> : ICascadeRepository<T, K>, IMemoryCacheRepository
{
}

/// <summary>
///     Represents a repository that uses the memory cache as a data source.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
/// <typeparam name="K">The type of the cache key.</typeparam>
public class MemoryCacheRepository<T, K> : CascadeRepository<T, K>, IMemoryCacheRepository<T, K>
{
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MemoryCacheRepository{T, K}" /> class
    ///     using the specified <see cref="IMemoryCache" /> and <see cref="MemoryCacheRepositoryOptions" />.
    /// </summary>
    /// <param name="memoryCache">The memory cache instance to be used.</param>
    /// <param name="options">The options specifying the TTL for the cache items.</param>
    public MemoryCacheRepository(IMemoryCache memoryCache, IOptions<MemoryCacheRepositoryOptions>? options)
        : base(options?.Value)
    {
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    protected override async Task<T?> CoreGet(K key, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_memoryCache.Get<T?>(KeyToKeyAdapter(key)!));
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_memoryCache.Get<IList<T>>(GetSetAllKey) ?? Array.Empty<T>() );
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetList<L>(L listId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_memoryCache.Get<IList<T>>(GetListKey(listId)) ?? Array.Empty<T>() );
    }

    /// <inheritdoc />
    protected override Task CoreSet(K key, T item, CancellationToken cancellationToken = default)
    {
        var expirationTime = CalculateExpirationTime();

        if (expirationTime == null)
            _memoryCache.Set(KeyToKeyAdapter(key)!, item);
        else
            _memoryCache.Set(KeyToKeyAdapter(key)!, item, expirationTime.Value);

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
    protected override Task CoreSetList<L>(L listId, IList<T> items, CancellationToken cancellationToken = default)
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
    protected override async Task CoreDelete(K key, CancellationToken cancellationToken = default)
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
    public const string ConfigPath = "Cascade:MemoryCache";
}