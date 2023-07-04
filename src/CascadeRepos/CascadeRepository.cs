using CascadeRepos.Extensions;

namespace CascadeRepos;

/// <summary>
///     Represents a base class for implementing a cascade repository.
/// </summary>
/// <typeparam name="T">The type of the items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of the keys used to access the items.</typeparam>
public abstract class CascadeRepository<T, TK> : ICascadeRepository<T, TK>
{
    private ICascadeRepository<T, TK>? _nextRepository;
    private bool _skipReading;
    private bool _skipWriting;
    
    /// <summary>
    /// The provider for retrieving the current date and time in UTC.
    /// </summary>
    private readonly IDateTimeProvider _dateTimeProvider;
    
    /// <summary>
    ///     The absolute expiration time for the items stored in the repository.
    /// </summary>
    protected DateTimeOffset? ExpirationTime;

    /// <summary>
    ///     The time to live (TTL) for the items stored in the repository.
    /// </summary>
    protected TimeSpan? TimeToLive;

    /// <summary>
    ///     Gets or sets the key adapter function used to adapt keys before accessing the repository.
    /// </summary>
    protected Func<TK, TK> KeyToKeyAdapter { get; private set; } = key => key;

    /// <summary>
    ///     Gets or sets the object to key adapter function used to adapt objects into keys before accessing the repository.
    /// </summary>
    protected Func<T, TK> ObjectToKeyAdapter { get; private set; } = _ => throw new InvalidOperationException();

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
    /// <param name="dateTimeProvider">The provider for retrieving the current date and time in UTC.</param>
    /// <param name="options">The optional configuration options for the Cascade Repository.</param>
    protected CascadeRepository(IDateTimeProvider dateTimeProvider, CascadeRepositoryOptions? options)
    {
        _dateTimeProvider = dateTimeProvider;
        TimeToLive = options?.TimeToLiveInSeconds is not null
            ? TimeSpan.FromSeconds(options.TimeToLiveInSeconds.Value)
            : null;

        IDictionary<string, int?>? timeToLiveInSecondsByEntity = options?.TimeToLiveInSecondsByEntity;
        if (timeToLiveInSecondsByEntity?.TryGetValue(typeof(T).Name, out var ttl) == true)
            TimeToLive = ttl is not null ? TimeSpan.FromSeconds((int)ttl) : null;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, TK>? FindRepository(Type repositoryType, uint index = 0)
    {
        return GetType() == repositoryType || GetType().GetInterfaces().Contains(repositoryType)
            ? index == 0 ? this : _nextRepository?.FindRepository(repositoryType, index - 1)
            : _nextRepository?.FindRepository(repositoryType, index);
    }

    /// <inheritdoc />
    public TR? FindRepository<TR>(uint index = 0) where TR : ICascadeRepository<T, TK>
    {
        return (TR?)FindRepository(typeof(TR), index);
    }

    /// <inheritdoc />
    public async Task<T?> Get(TK originalKey, bool updateDownStream = true,
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
    public async Task<IList<T>> GetList<TL>(TL listId, bool updateDownStream = true,
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
    public ICascadeRepository<T, TK>? GetNext()
    {
        return _nextRepository;
    }

    /// <inheritdoc />
    public async Task Refresh(TK originalKey, CancellationToken cancellationToken = default)
    {
        await (GetNext() is not null
            ? GetNext()!.Refresh(originalKey, cancellationToken)
            : Get(originalKey, true, cancellationToken));
    }

    /// <inheritdoc />
    public ICascadeRepository<T, TK> SetNext(ICascadeRepository<T, TK> nextRepository)
    {
        return _nextRepository = nextRepository;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, TK> SetTimeToLive(TimeSpan? time)
    {
        TimeToLive = time;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, TK> SetAbsoluteExpiration(DateTimeOffset expirationTime)
    {
        ExpirationTime = expirationTime;
        return this;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, TK> SkipRead<TR>()
    {
        if (GetType() == typeof(TR) || GetType().GetInterfaces().Contains(typeof(TR))) _skipReading = true;

        if (GetNext() is not null) GetNext()!.SkipRead<TR>();

        return this;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, TK> SkipReadThis()
    {
        _skipReading = true;
        return this;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, TK> SkipWrite<TR>()
    {
        if (GetType() == typeof(TR) || GetType().GetInterfaces().Contains(typeof(TR))) _skipWriting = true;

        if (GetNext() is not null) GetNext()!.SkipWrite<TR>();

        return this;
    }

    /// <inheritdoc />
    public ICascadeRepository<T, TK> SkipWriteThis()
    {
        _skipWriting = true;
        return this;
    }

    /// <inheritdoc />
    public async Task Set(TK key, T item, bool updateDownStream = false, CancellationToken cancellationToken = default)
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
    public async Task Delete(TK key, bool deleteDownStream = false, CancellationToken cancellationToken = default)
    {
        await CoreDelete(key, cancellationToken);

        if (GetNext() is null || !deleteDownStream) return;

        await GetNext()!.Delete(key, deleteDownStream, cancellationToken);
    }

    /// <inheritdoc />
    public ICascadeRepository<T, TK> AdaptKeyToKey(Func<TK, TK> keyAdapter)
    {
        KeyToKeyAdapter = keyAdapter;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, TK> AdaptObjectToKey(Func<T, TK> keyAdapter)
    {
        ObjectToKeyAdapter = keyAdapter;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, TK> AdaptGetSetAllKey(string keyAdapter)
    {
        GetSetAllKey = keyAdapter;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, TK> AdaptListPrefix(string prefix)
    {
        ListPrefix = prefix;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, TK> AdaptListKey(string key)
    {
        ListKey = key;
        return this;
    }

    /// <summary>
    /// Gets the final key to be used for Lists.
    /// </summary>
    /// <param name="listId"></param>
    /// <typeparam name="TL"></typeparam>
    /// <returns>The key to be used for Lists</returns>
    protected string GetListKey<TL>(TL listId) => ListKey ?? $"{ListPrefix}:{listId}";

    /// <summary>
    ///     Calculates the expiration time for a cache item based on the configured time-to-live and absolute expiration time.
    /// </summary>
    /// <returns>The calculated expiration time, or <c>null</c> if no expiration time is set.</returns>
    protected internal DateTimeOffset? CalculateExpirationTime()
    {
        DateTimeOffset? timeToLiveExpiration = TimeToLive != null
            ? _dateTimeProvider.GetUtcNow().Add(TimeToLive.Value)
            : null;

        if (ExpirationTime != null && (timeToLiveExpiration == null || timeToLiveExpiration > ExpirationTime))
            return ExpirationTime;

        return timeToLiveExpiration;
    }

    /// <summary>
    ///     Performs the core logic to retrieve the item from the repository.
    /// </summary>
    /// <param name="key">The adapted key to access the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retrieved item, or <c>null</c> if the item is not found.</returns>
    protected abstract Task<T?> CoreGet(TK key, CancellationToken cancellationToken = default);

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
    protected abstract Task<IList<T>> CoreGetList<TL>(TL listId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Performs the core logic to set the item in the repository.
    /// </summary>
    /// <param name="key">The key to access the item.</param>
    /// <param name="item">The item to set.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task CoreSet(TK key, T item, CancellationToken cancellationToken = default);

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
    protected abstract Task CoreSetList<TL>(TL listId, IList<T> items, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Performs the core logic to delete the item in the repository.
    /// </summary>
    /// <param name="key">The key to access the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task CoreDelete(TK key, CancellationToken cancellationToken = default);
}