using CascadeRepos.Extensions;
using Moq;

namespace CascadeRepos.UnitTests;

internal class TestCascadeRepository<T, TK> : CascadeRepository<T, TK>
{
    public TestCascadeRepository(TimeSpan? timeToLive = null, DateTimeOffset? expirationTime = null)
        : base(new DefaultDateTimeProvider(), null)
    {
        TimeToLive = timeToLive;
        ExpirationTime = expirationTime;
    }

    protected override Task<T?> CoreGet(TK key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task<IList<T>> CoreGetAll(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task<IList<T>> CoreGetList<TL>(TL listId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task CoreSet(TK key, T item, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task CoreSetAll(IList<T> items, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task<IList<T>> CoreSetList<TL>(TL listId, IList<T> items, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task CoreDelete(TK key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}