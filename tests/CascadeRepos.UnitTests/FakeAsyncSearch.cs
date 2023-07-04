using Amazon.DynamoDBv2.DataModel;

namespace CascadeRepos.UnitTests;

public class FakeAsyncSearch<T> : AsyncSearch<T>
{
    private List<T>? _results;

    public FakeAsyncSearch<T> SetResults(List<T> results)
    {
        _results = results;
        return this;
    }

    public override Task<List<T>> GetRemainingAsync(CancellationToken cancellationToken = new())
    {
        return Task.FromResult(_results ?? new List<T>());
    }
}