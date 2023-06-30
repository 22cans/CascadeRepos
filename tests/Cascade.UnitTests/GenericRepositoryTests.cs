using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cascade.UnitTests;

public class GenericRepositoryTests
{
    [Fact]
    public async Task Get_Returns_Item_When_Present()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        var storage = new Dictionary<string, SomeObject>
        {
            { key, value }
        };

        var repository = new GenericRepository<SomeObject, string>()
            .PrepareGet((k, _) => Task.FromResult(storage.TryGetValue(k, out var result) ? result : null));

        // Act
        var result = await repository.Get(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task Get_Returns_Default_Value_When_Item_Not_Found()
    {
        // Arrange
        var key = "cacheKey";

        var storage = new Dictionary<string, SomeObject>();

        var repository = new GenericRepository<SomeObject, string>()
            .PrepareGet((k, _) => Task.FromResult(storage.TryGetValue(k, out var result) ? result : null));

        // Act
        var result = await repository.Get(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Set_Adds_Item()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        var storage = new Dictionary<string, SomeObject>();

        var repository = new GenericRepository<SomeObject, string>()
            .PrepareSet((k, v, _) =>
            {
                storage[k] = v;
                return Task.CompletedTask;
            });

        // Act
        await repository.Set(key, value, false, CancellationToken.None);

        // Assert
        Assert.True(storage.ContainsKey(key));
        Assert.Equal(value, storage[key]);
    }

    [Fact]
    public async Task Delete_Removes_Item()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        var storage = new Dictionary<string, SomeObject> { { key, value } };

        var repository = new GenericRepository<SomeObject, string>()
            .PrepareDelete((k, _) =>
            {
                storage.Remove(key);
                return Task.CompletedTask;
            });

        // Act
        await repository.Delete(key, false, CancellationToken.None);

        // Assert
        Assert.False(storage.ContainsKey(key));
    }

    [Fact]
    public async Task GetAll_Returns_ListOfItems()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        var storage = new Dictionary<string, SomeObject>
        {
            { key, value }
        };

        var repository = new GenericRepository<SomeObject, string>()
            .PrepareGetAll(_ => Task.FromResult((IList<SomeObject>)storage.Values.ToList()));

        // Act
        var result = await repository.GetAll();

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(value, result[0]);
    }

    [Fact]
    public async Task GetAll_Returns_Empty_List()
    {
        // Arrange
        var storage = new Dictionary<string, SomeObject>();

        var repository = new GenericRepository<SomeObject, string>()
            .PrepareGetAll(_ => Task.FromResult((IList<SomeObject>)storage.Values.ToList()));

        // Act
        var result = await repository.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAll_Adds_Items_Using_CoreSetAll()
    {
        // Arrange
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var memoryCacheRepo = new MemoryCacheRepository<SomeObject, string>(memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));
        await memoryCacheRepo.SetAll(new List<SomeObject> { value });

        var storage = new Dictionary<string, SomeObject>();
        var repository = new GenericRepository<SomeObject, string>()
            .PrepareGetAll(_ => Task.FromResult((IList<SomeObject>)storage.Values.ToList()))
            .PrepareSetAll((values, _) =>
            {
                storage.Clear();
                foreach (var v in values)
                    storage.Add(v.Id, v);

                return Task.CompletedTask;
            });
        repository.SetNext(memoryCacheRepo);

        // Act
        var result = await repository.GetAll(true, CancellationToken.None);

        // Assert
        Assert.NotEmpty(storage.Values);
        Assert.Equal(value, storage[key]);
    }

    [Fact]
    public async Task Call_Without_Preparing_Fails()
    {
        const string key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };
        var repository = new GenericRepository<SomeObject, string>();

        await Assert.ThrowsAsync<NotImplementedException>(() => repository.Get(key));
        await Assert.ThrowsAsync<NotImplementedException>(() => repository.GetAll());
        await Assert.ThrowsAsync<NotImplementedException>(() => repository.Set(key, value));
        await Assert.ThrowsAsync<NotImplementedException>(() => repository.SetAll(new List<SomeObject> { value }));
        await Assert.ThrowsAsync<NotImplementedException>(() => repository.Delete(key));
    }
}