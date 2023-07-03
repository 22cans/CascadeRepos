namespace CascadeRepos.UnitTests;

internal class TestCascadeRepository<T, K> : CascadeRepository<T, K>
{
    public TestCascadeRepository(TimeSpan? timeToLive = null, DateTimeOffset? expirationTime = null)
        : base(null)
    {
        _timeToLive = timeToLive;
        _expirationTime = expirationTime;
    }

    protected override Task<T?> CoreGet(K key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task<IList<T>> CoreGetList<L>(L listId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task CoreSet(K key, T item, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task CoreSetAll(IList<T> items, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task<IList<T>> CoreSetList<L>(L listId, IList<T> items, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task CoreDelete(K key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}