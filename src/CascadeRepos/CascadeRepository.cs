using System.Text.Json;
using CascadeRepos.Extensions;
using Microsoft.Extensions.Logging;

namespace CascadeRepos;

/// <summary>
///     Represents a base class for implementing a cascade repository.
/// </summary>
/// <typeparam name="T">The type of the items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of the keys used to access the items.</typeparam>
public abstract class CascadeRepository<T, TK> : ICascadeRepository<T, TK>
{
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <summary>
    ///     The type of expiration for the entity in the repository.
    /// </summary>
    protected readonly ExpirationType ExpirationType;

    /// <summary>
    ///     The logger instance used for logging.
    /// </summary>
    protected readonly ILogger<CascadeRepository<T, TK>> Logger;

    private string? _listKey;
    private string _listPrefix = typeof(T).Name;
    private ICascadeRepository<T, TK>? _nextRepository;
    private bool _skipReading;
    private bool _skipWriting;

    /// <summary>
    ///     The absolute expiration time for the items stored in the repository.
    /// </summary>
    protected DateTimeOffset? ExpirationTime;

    /// <summary>
    ///     The time to live (TTL) for the items stored in the repository.
    /// </summary>
    protected TimeSpan? TimeToLive;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CascadeRepository{T, K}" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="dateTimeProvider">The provider for retrieving the current date and time in UTC.</param>
    /// <param name="options">The optional configuration options for the Cascade Repository.</param>
    protected CascadeRepository(ILogger<CascadeRepository<T, TK>> logger, IDateTimeProvider dateTimeProvider,
        CascadeRepositoryOptions? options)
    {
        Logger = logger;
        _dateTimeProvider = dateTimeProvider;
        ExpirationType = options?.DefaultExpirationType ?? ExpirationType.Absolute;
        TimeToLive = options?.TimeToLiveInSeconds is not null
            ? TimeSpan.FromSeconds(options.TimeToLiveInSeconds.Value)
            : null;

        var timeToLiveInSecondsByEntity = options?.TimeToLiveInSecondsByEntity;
        if (timeToLiveInSecondsByEntity?.TryGetValue(typeof(T).Name, out var entityOptions) != true) return;

        ExpirationType = entityOptions?.ExpirationType ?? ExpirationType;
        TimeToLive = entityOptions?.TimeToLiveInSeconds != null
            ? TimeSpan.FromSeconds((int)entityOptions?.TimeToLiveInSeconds!)
            : TimeToLive;
    }

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
            switch (_skipReading)
            {
                case true:
                    LogSkipReading();
                    break;

                case false:
                {
                    LogGettingData();
                    var value = await CoreGet(originalKey, cancellationToken);
                    LogGotData(value);
                    if (value is not null) return value;
                    break;
                }
            }

            if (GetNext() is null) return default;

            var next = await GetNext()!.Get(originalKey, updateDownStream, cancellationToken);
            if (next is null || !updateDownStream) return next;

            switch (_skipWriting)
            {
                case true:
                    LogSkipWriting();
                    break;

                case false:
                    LogSettingData();
                    await CoreSet(originalKey, next, cancellationToken);
                    LogSetData(next);
                    break;
            }

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
            switch (_skipReading)
            {
                case true:
                    LogSkipReading();
                    break;

                case false:
                {
                    LogGettingData();
                    var value = await CoreGetAll(cancellationToken);

                    if (value.Any()) return value;
                    break;
                }
            }

            if (GetNext() is null) return Array.Empty<T>();

            var next = await GetNext()!.GetAll(updateDownStream, cancellationToken);
            if (!next.Any() || !updateDownStream) return next;

            switch (_skipWriting)
            {
                case true:
                    LogSkipWriting();
                    break;

                case false:
                    LogSettingData();
                    await CoreSetAll(next, cancellationToken);
                    break;
            }

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
            switch (_skipReading)
            {
                case true:
                    LogSkipReading();
                    break;

                case false:
                {
                    LogGettingData();
                    var value = await CoreGetList(listId, cancellationToken);

                    if (value.Any()) return value;
                    break;
                }
            }

            if (GetNext() is null) return Array.Empty<T>();

            var next = await GetNext()!.GetList(listId, updateDownStream, cancellationToken);
            if (!next.Any() || !updateDownStream) return next;

            switch (_skipWriting)
            {
                case true:
                    LogSkipWriting();
                    break;

                case false:
                    LogSettingData();
                    await CoreSetList(listId, next, cancellationToken);
                    break;
            }

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
        await Get(originalKey, true, cancellationToken);
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
            switch (_skipWriting)
            {
                case true:
                    LogSkipWriting();
                    break;

                case false:
                    LogSettingData();
                    await CoreSet(key, item, cancellationToken);
                    LogSetData(item);
                    break;
            }

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
            switch (_skipWriting)
            {
                case true:
                    LogSkipWriting();
                    break;

                case false:
                    LogSettingData();
                    await CoreSetAll(items, cancellationToken);
                    break;
            }

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
        _listPrefix = prefix;
        return this;
    }

    /// <inheritdoc />
    public virtual ICascadeRepository<T, TK> AdaptListKey(string key)
    {
        _listKey = key;
        return this;
    }

    /// <summary>
    ///     Gets the final key to be used for Lists.
    /// </summary>
    /// <param name="listId"></param>
    /// <typeparam name="TL"></typeparam>
    /// <returns>The key to be used for Lists</returns>
    protected string GetListKey<TL>(TL listId)
    {
        return _listKey ?? $"{_listPrefix}:{listId}";
    }

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

    private void LogSkipReading()
    {
        Logger.LogDebug("{ThreadId}: Skip reading '{Entity}' data from {Name}",
            Environment.CurrentManagedThreadId, typeof(T).Name, GetType().Name);
    }

    private void LogSkipWriting()
    {
        Logger.LogDebug("{ThreadId}: Skip writing '{Entity}' data for {Name}",
            Environment.CurrentManagedThreadId, typeof(T).Name, GetType().Name);
    }

    private void LogGettingData()
    {
        Logger.LogDebug("{ThreadId}: Getting '{Entity}' data from '{Name}'",
            Environment.CurrentManagedThreadId, typeof(T).Name, GetType().Name);
    }

    private void LogGotData(T? data)
    {
        Logger.LogDebug("{ThreadId}: '{Entity}' data got from '{Name}': '{Data}'",
            Environment.CurrentManagedThreadId, typeof(T).Name, GetType().Name, 
            data is null ? string.Empty: JsonSerializer.Serialize(data));
    }

    private void LogSettingData()
    {
        Logger.LogDebug("{ThreadId}: Setting '{Entity}' data for '{Name}'",
            Environment.CurrentManagedThreadId, typeof(T).Name, GetType().Name);
    }
    
    private void LogSetData<TA>(TA? data)
    {
        Logger.LogDebug("{ThreadId}: '{Entity}' data set for '{Name}: '{Data}'",
            Environment.CurrentManagedThreadId, typeof(T).Name, GetType().Name, 
            data is null ? string.Empty: JsonSerializer.Serialize(data));
    }
}