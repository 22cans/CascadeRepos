using CascadeRepos.Extensions;
using Microsoft.Extensions.Logging;

namespace CascadeRepos;

/// <summary>
///     Represents an interface for a generic repository.
/// </summary>
public interface IGenericRepository
{
}

/// <summary>
///     Represents an interface for a generic repository.
/// </summary>
/// <typeparam name="T">The type of items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of keys used to access the items.</typeparam>
public interface IGenericRepository<T, TK> : ICascadeRepository<T, TK>, IGenericRepository
{
    /// <summary>
    ///     Prepares the CoreGet method with the specified function.
    /// </summary>
    /// <param name="coreGet">The function to be used for retrieving items from the repository.</param>
    /// <returns>The current instance of the <see cref="GenericRepository{T, K}" /> class.</returns>
    IGenericRepository<T, TK> PrepareGet(Func<TK, CancellationToken, Task<T?>> coreGet);

    /// <summary>
    ///     Prepares the CoreSet method with the specified function.
    /// </summary>
    /// <param name="coreSet">The function to be used for setting items in the repository.</param>
    /// <returns>The current instance of the <see cref="GenericRepository{T, K}" /> class.</returns>
    IGenericRepository<T, TK> PrepareSet(Func<TK, T, CancellationToken, Task> coreSet);

    /// <summary>
    ///     Prepares the repository for executing the GetAll operation by setting the core implementation delegate.
    /// </summary>
    /// <param name="coreGetAll">The delegate representing the core implementation of the GetAll operation.</param>
    /// <returns>The modified repository instance.</returns>
    public IGenericRepository<T, TK> PrepareGetAll(Func<CancellationToken, Task<IList<T>>> coreGetAll);

    /// <summary>
    ///     Prepares the repository for executing the SetAll operation by setting the core implementation delegate.
    /// </summary>
    /// <param name="coreSetAll">The delegate representing the core implementation of the SetAll operation.</param>
    /// <returns>The modified repository instance.</returns>
    public IGenericRepository<T, TK> PrepareSetAll(Func<IList<T>, CancellationToken, Task> coreSetAll);

    /// <summary>
    ///     Prepares the CoreDelete method with the specified function.
    /// </summary>
    /// <param name="coreDelete">The function to be used for deleting items in the repository.</param>
    /// <returns>The current instance of the <see cref="GenericRepository{T, K}" /> class.</returns>
    IGenericRepository<T, TK> PrepareDelete(Func<TK, CancellationToken, Task> coreDelete);
}

/// <summary>
///     Represents a generic repository for HTTP calls, services, and other types of data.
/// </summary>
/// <typeparam name="T">The type of the items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of the keys used to retrieve items from the repository.</typeparam>
public class GenericRepository<T, TK> : CascadeRepository<T, TK>, IGenericRepository<T, TK>
{
    private Func<TK, CancellationToken, Task> _coreDelete = (_, _) => throw new NotImplementedException();
    private Func<TK, CancellationToken, Task<T?>> _coreGet = (_, _) => throw new NotImplementedException();
    private Func<CancellationToken, Task<IList<T>>> _coreGetAll = _ => throw new NotImplementedException();
    private Func<TK, T, CancellationToken, Task> _coreSet = (_, _, _) => throw new NotImplementedException();
    private Func<IList<T>, CancellationToken, Task> _coreSetAll = (_, _) => throw new NotImplementedException();

    /// <summary>
    ///     Initializes a new instance of the <see cref="GenericRepository{T, K}" /> class.
    /// </summary>
    /// <param name="logger">The logger instance used for logging.</param>
    /// <param name="dateTimeProvider">The provider for retrieving the current date and time in UTC.</param>
    public GenericRepository(ILogger<CascadeRepository<T, TK>> logger, IDateTimeProvider dateTimeProvider) : base(
        logger, dateTimeProvider, null)
    {
    }

    /// <inheritdoc />
    public IGenericRepository<T, TK> PrepareGet(Func<TK, CancellationToken, Task<T?>> coreGet)
    {
        _coreGet = coreGet;
        return this;
    }

    /// <inheritdoc />
    public IGenericRepository<T, TK> PrepareSet(Func<TK, T, CancellationToken, Task> coreSet)
    {
        _coreSet = coreSet;
        return this;
    }

    /// <inheritdoc />
    public IGenericRepository<T, TK> PrepareGetAll(Func<CancellationToken, Task<IList<T>>> coreGetAll)
    {
        _coreGetAll = coreGetAll;
        return this;
    }

    /// <inheritdoc />
    public IGenericRepository<T, TK> PrepareSetAll(Func<IList<T>, CancellationToken, Task> coreSetAll)
    {
        _coreSetAll = coreSetAll;
        return this;
    }

    /// <inheritdoc />
    public IGenericRepository<T, TK> PrepareDelete(Func<TK, CancellationToken, Task> coreDelete)
    {
        _coreDelete = coreDelete;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<T?> CoreGet(TK key, CancellationToken cancellationToken = default)
    {
        return await _coreGet(KeyToKeyAdapter(key), cancellationToken);
    }

    /// <inheritdoc />
    protected override Task<IList<T>> CoreGetList<TL>(TL listId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    protected override async Task CoreSet(TK key, T item, CancellationToken cancellationToken = default)
    {
        await _coreSet(KeyToKeyAdapter(key), item, cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default)
    {
        return await _coreGetAll(cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task CoreSetAll(IList<T> items, CancellationToken cancellationToken = default)
    {
        await _coreSetAll(items, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task CoreSetList<TL>(TL listId, IList<T> items, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    protected override async Task CoreDelete(TK key, CancellationToken cancellationToken = default)
    {
        await _coreDelete(KeyToKeyAdapter(key), cancellationToken);
    }
}