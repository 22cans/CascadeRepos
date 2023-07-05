using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2.DataModel;
using CascadeRepos.Extensions;
using Microsoft.Extensions.Logging;

namespace CascadeRepos;

/// <summary>
///     Represents an interface for a DynamoDB repository.
/// </summary>
public interface IDynamoDbRepository
{
}

/// <summary>
///     Represents an interface for a DynamoDB repository.
/// </summary>
/// <typeparam name="T">The type of items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of keys used to access the items.</typeparam>
public interface IDynamoDbRepository<T, TK> : ICascadeRepository<T, TK>, IDynamoDbRepository
{
    /// <summary>
    ///     Sets the range key adapter function to adapt the original key.
    /// </summary>
    /// <param name="rangeKeyAdapter">The adapter function to convert the key to the range key.</param>
    /// <returns>The current <see cref="DynamoDbRepository{T, K}" /> instance.</returns>
    IDynamoDbRepository<T, TK> SetRangeKey(Func<TK, object> rangeKeyAdapter);

    /// <summary>
    ///     Sets the OperationConfig in Dynamo.
    ///     This allows you to set things like Index.
    /// </summary>
    /// <param name="operationConfig">The <see cref="DynamoDBOperationConfig" /> to be used.</param>
    /// <returns>The current <see cref="DynamoDbRepository{T, K}" /> instance.</returns>
    IDynamoDbRepository<T, TK> SetOperationConfig(DynamoDBOperationConfig? operationConfig);
}

/// <summary>
///     Represents a repository implementation using DynamoDB as the underlying storage.
/// </summary>
/// <typeparam name="T">The type of the items stored in the repository.</typeparam>
/// <typeparam name="TK">The type of the keys used to access the items.</typeparam>
public class DynamoDbRepository<T, TK> : CascadeRepository<T, TK>, IDynamoDbRepository<T, TK>
{
    private readonly IDynamoDBContext _dbContext;
    private DynamoDBOperationConfig? _operationConfig;
    private Func<TK, object?> _rangeKeyAdapter = _ => null;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamoDbRepository{T,K}" /> class.
    /// </summary>
    /// <param name="logger">The logger instance used for logging.</param>
    /// <param name="dateTimeProvider">The provider for retrieving the current date and time in UTC.</param>
    /// <param name="dbContext">The DynamoDB context used to interact with the database.</param>
    public DynamoDbRepository(ILogger<CascadeRepository<T, TK>> logger, IDateTimeProvider dateTimeProvider,
        IDynamoDBContext dbContext) : base(logger, dateTimeProvider, null)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public IDynamoDbRepository<T, TK> SetRangeKey(Func<TK, object> rangeKeyAdapter)
    {
        _rangeKeyAdapter = rangeKeyAdapter;
        return this;
    }

    /// <inheritdoc />
    public IDynamoDbRepository<T, TK> SetOperationConfig(DynamoDBOperationConfig? operationConfig)
    {
        _operationConfig = operationConfig;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<T?> CoreGet(TK key, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LoadAsync<T>(KeyToKeyAdapter(key), _rangeKeyAdapter(key), _operationConfig,
            cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScanAsync<T>(null, _operationConfig).GetRemainingAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task<IList<T>> CoreGetList<TL>(TL listId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueryAsync<T>(listId, _operationConfig).GetRemainingAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     DynamoDB does not directly requires the HashKey and/or SortKey for doing saves.
    ///     The save in DynamoDB is done by simply passing the item, which DynamoDB internally resolves the keys.
    ///     Based on that, a RangeKey for saving is not required, and the Key is not really used, but discarded.
    /// </remarks>
    protected override async Task CoreSet(TK _, T item, CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    [ExcludeFromCodeCoverage(Justification =
        "The method `dbContext.CreateBatchWrite` is not mockable, which blocks the tests.")]
    protected override async Task CoreSetAll(IList<T> items, CancellationToken cancellationToken = default)
    {
        var batch = _dbContext.CreateBatchWrite<T>(_operationConfig);
        batch.AddPutItems(items);
        await batch.ExecuteAsync(cancellationToken);
    }

    /// <inheritdoc />
    [ExcludeFromCodeCoverage(Justification =
        "The method `dbContext.CreateBatchWrite` is not mockable, which blocks the tests.")]
    protected override async Task CoreSetList<TL>(TL _, IList<T> items, CancellationToken cancellationToken = default)
    {
        var batch = _dbContext.CreateBatchWrite<T>(_operationConfig);
        batch.AddPutItems(items);
        await batch.ExecuteAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task CoreDelete(TK key, CancellationToken cancellationToken = default)
    {
        await _dbContext.DeleteAsync<T>(KeyToKeyAdapter(key), _rangeKeyAdapter(key), _operationConfig,
            cancellationToken);
    }
}