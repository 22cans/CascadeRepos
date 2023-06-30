namespace Cascade;

/// <summary>
///     Represents a base class for implementing a cascade repository.
/// </summary>
/// <typeparam name="T">The type of the items stored in the repository.</typeparam>
/// <typeparam name="K">The type of the keys used to access the items.</typeparam>
public abstract class CascadeRepository<T, K> : ICascadeRepository<T, K>
{
    private ICascadeRepository<T, K>? _nextRepository;
    private bool _skipReading;
    private bool _skipWriting;

    /// <summary>
    ///     The absolute expiration time for the items stored in the repository.
    /// </summary>
    protected DateTimeOffset? _expirationTime;

    /// <summary>
    ///     The time to live (TTL) for the items stored in the repository.
    /// </summary>
    protected TimeSpan? _timeToLive;

    /// <summary>
    ///     The time to live (TTL) per entity, for the cached items, in seconds.
    /// </summary>
    protected IDictionary<string, int?>? _timeToLiveInSecondsByEntity;

    /// <summary>
    ///     Gets or sets the key adapter function used to adapt keys before accessing the repository.
    /// </summary>
    protected Func<K, K> KeyToKeyAdapter { get; private set; } = key => key;

    /// <summary>
    ///     Gets or sets the object to key adapter function used to adapt objects into keys before accessing the repository.
    /// </summary>
    protected Func<T, K> ObjectToKeyAdapter { get; private set; } = _ => throw new InvalidOperationException();

    /// <summary>
    ///     Gets or sets the key used in GetAll/SetAll methods when accessing the repository.
    /// </summary>
    protected string GetSetAllKey { get; private set; } = $"{typeof(T).Name}_List";

    /// <summary>
    ///     Gets or sets a key prefix used in GetList method when accessing the repository. Only used if the ListKey is not defined.
    /// </summary>
    protected string ListPrefix { get; private set; } = typeof(T).Name;

    /// <summary>
    ///     Gets or sets a definitive key used in GetList method when accessing the repository.
    /// </summary>
    protected string? ListKey { get; private set; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CascadeRepository{T, K}" /> class.
    /// </summary>
    /// <param name="options"></param>
    protected CascadeRepository(CascadeRepositoryOptions? options)
    {
        _timeToLive = options?.TimeToLiveInSeconds is not null
            ? TimeSpan.FromSeconds(options.TimeToLiveInSeconds.Value)
            : null;

        _timeToLiveInSecondsByEntity = options?.TimeToLiveInSecondsByEntity;
        if (_timeToLiveInSecondsByEntity?.TryGetValue(typeof(T).Name, out var ttl) == true)
            _timeToLive = ttl is not null ? TimeSpan.FromSeconds((int)ttl) : null;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, K>? FindRepository(Type repositoryType, uint index = 0)
    {
        return GetType() == repositoryType || GetType().GetInterfaces().Contains(repositoryType)
            ? index == 0 ? this : _nextRepository?.FindRepository(repositoryType, index - 1)
            : _nextRepository?.FindRepository(repositoryType, index);
    }

    /// <inheritdoc />
    public R? FindRepository<R>(uint index = 0) where R : ICascadeRepository<T, K>
    {
        return (R?)FindRepository(typeof(R), index);
    }

    /// <inheritdoc />
    public async Task<T?> Get(K originalKey, bool updateDownStream = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_skipReading)
            {
                var value = await CoreGet(originalKey, cancellationToken);

                if (value is not null) return value;
            }

            if (GetNext() is null) return default;

            var next = await GetNext()!.Get(originalKey, updateDownStream, cancellationToken);

            if (next is not null && !_skipWriting && updateDownStream)
                await CoreSet(originalKey, next, cancellationToken);

            return next;
        }
        finally
        {
            _skipReading = false;
        }
    }

    /// <inheritdoc />
    public async Task<IList<T>> GetAll(bool updateDownStream = true, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_skipReading)
            {
                var value = await CoreGetAll(cancellationToken);

                if (value.Any()) return value;
            }

            if (GetNext() is null) return Array.Empty<T>();

            var next = await GetNext()!.GetAll(updateDownStream, cancellationToken);

            if (next.Any() && !_skipWriting && updateDownStream) await CoreSetAll(next, cancellationToken);

            return next;
        }
        finally
        {
            _skipReading = false;
        }
    }

    /// <inheritdoc />
    public async Task<IList<T>> GetList<L>(L listId, bool updateDownStream = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_skipReading)
            {
                var value = await CoreGetList(listId, cancellationToken);

                if (value.Any()) return value;
            }

            if (GetNext() is null) return Array.Empty<T>();

            var next = await GetNext()!.GetList(listId, updateDownStream, cancellationToken);

            if (next.Any() && !_skipWriting && updateDownStream) await CoreSetList(listId, next, cancellationToken);

            return next;
        }
        finally
        {
            _skipReading = false;
        }
    }

    /// <inheritdoc />
    public ICascadeRepository<T, K>? GetNext()
    {
        return _nextRepository;
    }

    /// <inheritdoc />
    public async Task Refresh(K originalKey, CancellationToken cancellationToken = default)
    {
        await (GetNext() is not null
            ? GetNext()!.Refresh(originalKey, cancellationToken)
            : Get(originalKey, true, cancellationToken));
    }

    /// <inheritdoc />
    public ICascadeRepository<T, K> SetNext(ICascadeRepository<T, K> nextRepository)
    {
        return _nextRepository = nextRepository;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, K> SetTimeToLive(TimeSpan? time)
    {
        _timeToLive = time;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, K> SetAbsoluteExpiration(DateTimeOffset expirationTime)
    {
        _expirationTime = expirationTime;
        return this;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, K> SkipRead<R>()
    {
        if (GetType() == typeof(R) || GetType().GetInterfaces().Contains(typeof(R))) _skipReading = true;

        if (GetNext() is not null) GetNext()!.SkipRead<R>();

        return this;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, K> SkipReadThis()
    {
        _skipReading = true;
        return this;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, K> SkipWrite<R>()
    {
        if (GetType() == typeof(R) || GetType().GetInterfaces().Contains(typeof(R))) _skipWriting = true;

        if (GetNext() is not null) GetNext()!.SkipWrite<R>();

        return this;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, K> SkipWriteThis()
    {
        _skipWriting = true;
        return this;
    }

    /// <inheritdoc />
    public async Task Set(K key, T item, bool updateDownStream = false, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_skipWriting) await CoreSet(key, item, cancellationToken);

            if (GetNext() is null || !updateDownStream) return;

            await GetNext()!.Set(key, item, updateDownStream, cancellationToken);
        }
        finally
        {
            _skipWriting = false;
        }
    }

    /// <inheritdoc />
    public async Task SetAll(List<T> items, bool updateDownStream = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_skipWriting) await CoreSetAll(items, cancellationToken);

            if (GetNext() is null || !updateDownStream) return;

            await GetNext()!.SetAll(items, updateDownStream, cancellationToken);
        }
        finally
        {
            _skipWriting = false;
        }
    }

    /// <inheritdoc />
    public async Task Delete(K key, bool deleteDownStream = false, CancellationToken cancellationToken = default)
    {
        await CoreDelete(key, cancellationToken);

        if (GetNext() is null || !deleteDownStream) return;

        await GetNext()!.Delete(key, deleteDownStream, cancellationToken);
    }

    /// <inheritdoc />
    public ICascadeRepository<T, K> AdaptKeyToKey(Func<K, K> keyAdapter)
    {
        KeyToKeyAdapter = keyAdapter;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, K> AdaptObjectToKey(Func<T, K> keyAdapter)
    {
        ObjectToKeyAdapter = keyAdapter;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, K> AdaptGetSetAllKey(string keyAdapter)
    {
        GetSetAllKey = keyAdapter;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, K> AdaptListPrefix(string prefix)
    {
        ListPrefix = prefix;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, K> AdaptListKey(string key)
    {
        ListKey = key;
        return this;
    }

    /// <summary>
    /// Gets the final key to be used for Lists.
    /// </summary>
    /// <param name="listId"></param>
    /// <typeparam name="L"></typeparam>
    /// <returns>The key to be used for Lists</returns>
    protected string GetListKey<L>(L listId) => ListKey ?? $"{ListPrefix}:{listId}";

    /// <summary>
    ///     Calculates the expiration time for a cache item based on the configured time-to-live and absolute expiration time.
    /// </summary>
    /// <returns>The calculated expiration time, or <c>null</c> if no expiration time is set.</returns>
    protected internal DateTimeOffset? CalculateExpirationTime()
    {
        DateTimeOffset? timeToLiveExpiration = _timeToLive != null
            ? DateTimeOffset.UtcNow.Add(_timeToLive.Value)
            : null;

        if (_expirationTime != null && (timeToLiveExpiration == null || timeToLiveExpiration > _expirationTime))
            return _expirationTime;

        return timeToLiveExpiration;
    }

    /// <summary>
    ///     Performs the core logic to retrieve the item from the repository.
    /// </summary>
    /// <param name="key">The adapted key to access the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retrieved item, or <c>null</c> if the item is not found.</returns>
    protected abstract Task<T?> CoreGet(K key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Performs the core logic to retrieve all objects from the repository.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of all objects retrieved from the repository.</returns>
    protected abstract Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Performs the core logic to retrieve all objects from the repository.
    /// </summary>
    /// <param name="listId">The list id to be retrieved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of all objects retrieved from the repository.</returns>
    protected abstract Task<IList<T>> CoreGetList<L>(L listId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Performs the core logic to set the item in the repository.
    /// </summary>
    /// <param name="key">The key to access the item.</param>
    /// <param name="item">The item to set.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task CoreSet(K key, T item, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Performs the core logic to set all objects from the repository.
    /// </summary>
    /// <param name="items">The list of items to set in the repository.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task CoreSetAll(IList<T> items, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Performs the core logic to set all objects from the repository.
    /// </summary>
    /// <param name="listId">The list id to be retrieved.</param>
    /// <param name="items">The list of items to set in the repository.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of all objects retrieved from the repository.</returns>
    protected abstract Task CoreSetList<L>(L listId, IList<T> items, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Performs the core logic to delete the item in the repository.
    /// </summary>
    /// <param name="key">The key to access the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task CoreDelete(K key, CancellationToken cancellationToken = default);
}