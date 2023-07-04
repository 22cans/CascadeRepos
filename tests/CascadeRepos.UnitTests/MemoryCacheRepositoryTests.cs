using CascadeRepos.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CascadeRepos.UnitTests;

public class MemoryCacheRepositoryTests
{
    [Fact]
    public async Task Get_Returns_Item_When_Present()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };
        memoryCache.Set(key, value);

        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        // Act
        var result = await repository.Get(key);

        // Assert
        Assert.Equivalent(value, result);
    }

    [Fact]
    public async Task Get_Returns_Default_Value_When_Item_Not_Found()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheKey = "nonExistentKey";

        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        // Act
        var result = await repository.Get(cacheKey);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Set_Adds_Item()
    {
        // Arrange
        var memoryCacheMock = new Mock<IMemoryCache>();
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        memoryCacheMock
            .Setup(m => m.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());

        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        // Act
        await repository.Set(key, value, false, CancellationToken.None);

        // Assert
        memoryCacheMock.Verify(c => c.CreateEntry(key), Times.Once);
    }

    [Fact]
    public async Task Set_Sets_Item_With_Sliding_Expiration_When_Enabled()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions
                { TimeToLiveInSeconds = 1, DefaultExpirationType = ExpirationType.Sliding }));

        // Act
        await repository.Set(key, value);
        await Task.Delay(800);
        await repository.Get(key);
        await Task.Delay(800);
        var slidingResult = await repository.Get(key);
        await Task.Delay(1200);
        var expiredResult = await repository.Get(key);

        // Assert
        Assert.Equivalent(value, slidingResult);
        Assert.Null(expiredResult);
    }

    [Fact]
    public async Task Set_Sets_Item_With_Sliding_Expiration_By_Dictionary_When_Enabled()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var key = "cacheKey";
        var value = new SomeObject
        {
            Id = key,
            Name = "Some Name"
        };

        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions
            {
                TimeToLiveInSecondsByEntity =
                    new Dictionary<string, (int? TimeToLiveInSeconds, ExpirationType? ExpirationType)?>
                    {
                        { nameof(SomeObject), (1, ExpirationType.Sliding) }
                    }
            }));

        // Act
        await repository.Set(key, value);
        await Task.Delay(800);
        await repository.Get(key);
        await Task.Delay(800);
        var slidingResult = await repository.Get(key);
        await Task.Delay(1200);
        var expiredResult = await repository.Get(key);

        // Assert
        Assert.Equivalent(value, slidingResult);
        Assert.Null(expiredResult);
    }

    [Fact]
    public async Task Delete_Removes_Item()
    {
        // Arrange
        var memoryCacheMock = new Mock<IMemoryCache>();
        var key = "cacheKey";
        memoryCacheMock
            .Setup(m => m.Remove(It.IsAny<object>()));
        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCacheMock.Object,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        // Act
        await repository.Delete(key, false, CancellationToken.None);

        // Assert
        memoryCacheMock.Verify(c => c.Remove(key), Times.Once);
    }

    [Fact]
    public async Task GetAll_Returns_ListOfItems()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var value = new SomeObject
        {
            Id = "cacheKey",
            Name = "Some Name"
        };
        memoryCache.Set($"{nameof(SomeObject)}_List", new List<SomeObject> { value });
        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

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
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

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

        var storage = new Dictionary<string, SomeObject>
        {
            { key, value }
        };
        var genericRepository = new GenericRepository<SomeObject, string>(Mock.Of<IDateTimeProvider>())
            .PrepareGetAll(_ => Task.FromResult((IList<SomeObject>)storage.Values.ToList()))
            .PrepareSetAll((values, _) =>
            {
                storage.Clear();
                foreach (var v in values)
                    storage.Add(v.Id, v);

                return Task.CompletedTask;
            });

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var memoryCacheRepo = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));
        memoryCacheRepo
            .AdaptObjectToKey(x => x.Id)
            .SetNext(genericRepository);

        // Act
        var result = await memoryCacheRepo.GetAll(true, CancellationToken.None);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(result[0], storage[key]);
    }

    [Fact]
    public async Task GetAll_Adds_Items_Using_CoreSetAll_With_GetSetAllKey()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var value = new SomeObject
        {
            Id = "cacheKey",
            Name = "Some Name"
        };
        const string key = "TESTING_KEY";
        memoryCache.Set(key, new List<SomeObject> { value });
        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        repository.AdaptGetSetAllKey(key);
        // Act
        var result = await repository.GetAll();

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(value, result[0]);
    }

    [Fact]
    public async Task Get_When_Expired_Returns_From_Next_Repo()
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
        var genericRepository = new GenericRepository<SomeObject, string>(Mock.Of<IDateTimeProvider>())
            .PrepareGet((k, _) => Task.FromResult(storage[k])!)
            .PrepareSet((k, item, _) =>
            {
                storage.TryAdd(k, item);
                return Task.CompletedTask;
            });

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var memoryCacheRepo = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));
        memoryCacheRepo
            .AdaptObjectToKey(x => x.Id)
            .SetTimeToLive(TimeSpan.FromSeconds(1))
            .SetNext(genericRepository);

        await memoryCacheRepo
            .Set(key, value, true, CancellationToken.None);

        await Task.Delay(1100);

        // Act
        var result = await memoryCacheRepo.Get(key, true, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(result, value);
    }

    [Fact]
    public async Task GetList_Returns_ListOfItems()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        const string listId = "ListId";
        var value = new SomeObject
        {
            Id = "cacheKey",
            Name = "Some Name"
        };
        memoryCache.Set($"{nameof(SomeObject)}:ListId", new List<SomeObject> { value });
        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        // Act
        var result = await repository.GetList(listId);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(value, result[0]);
    }

    [Fact]
    public async Task GetList_Returns_Empty_List()
    {
        // Arrange
        const string listId = "ListId";
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var repository = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        // Act
        var result = await repository.GetList(listId);

        // Assert
        Assert.Empty(result);
    }
}