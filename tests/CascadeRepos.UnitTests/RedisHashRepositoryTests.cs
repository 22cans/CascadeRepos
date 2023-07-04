using CascadeRepos.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using StackExchange.Redis;
using Xunit;

namespace CascadeRepos.UnitTests;

public class RedisHashRepositoryTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMultiplexerMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisHashRepository<SomeObject, string> _repository;

    public RedisHashRepositoryTests()
    {
        _databaseMock = new Mock<IDatabase>();
        _connectionMultiplexerMock = new Mock<IConnectionMultiplexer>();
        _connectionMultiplexerMock
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _repository = new RedisHashRepository<SomeObject, string>(Mock.Of<IDateTimeProvider>(),
            _connectionMultiplexerMock.Object);
    }

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
        _databaseMock
            .Setup(d => d.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonConvert.SerializeObject(value));

        // Act
        var result = await _repository.Get(key);

        // Assert
        Assert.Equivalent(value, result);
    }

    [Fact]
    public async Task Get_Returns_Default_Value_When_Item_Not_Found()
    {
        // Arrange
        var key = "cacheKey";
        _databaseMock
            .Setup(d => d.KeyDeleteAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _repository.Get(key);

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

        // Act
        await _repository
            .AdaptKeyToKey(k => k.ToString())
            .Set(key, value, false, CancellationToken.None);

        // Assert
        _databaseMock.Verify(
            d => d.HashSetAsync(It.IsAny<RedisKey>(), key, It.IsAny<RedisValue>(), When.Always, CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task Delete_Removes_Item()
    {
        // Arrange
        var key = "cacheKey";

        // Act
        await _repository
            .AdaptKeyToKey(k => k.ToString())
            .Delete(key, false, CancellationToken.None);

        // Assert
        _databaseMock.Verify(d => d.HashDeleteAsync(It.IsAny<RedisKey>(), key, CommandFlags.None), Times.Once);
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
        _databaseMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(new[] { new HashEntry(nameof(SomeObject), JsonConvert.SerializeObject(value)) })
            .Verifiable();

        // Act
        var result = await _repository.GetAll();

        // Assert
        Assert.NotEmpty(result);
        Assert.Equivalent(value, result[0]);
        _databaseMock.Verify();
    }

    [Fact]
    public async Task GetAll_Returns_Empty_List()
    {
        // Arrange
        _databaseMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<HashEntry>())
            .Verifiable();

        // Act
        var result = await _repository.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAll_Adds_Items_Using_CoreSetAll()
    {
        // Arrange
        var value = new SomeObject
        {
            Id = "cacheKey",
            Name = "Some Name"
        };

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var memoryCacheRepo = new MemoryCacheRepository<SomeObject, string>(
            new DefaultDateTimeProvider(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));
        await memoryCacheRepo.SetAll(new List<SomeObject> { value });

        _databaseMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<HashEntry>());
        _repository.SetNext(memoryCacheRepo);

        // Act
        var result = await _repository
            .AdaptRedisKey(() => nameof(SomeObject))
            .AdaptObjectToKey(obj => obj.Id)
            .GetAll(true, CancellationToken.None);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(value, result[0]);
        _databaseMock.Verify(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None), Times.Once);
    }


    [Fact]
    public async Task GetList_Returns_ListOfItems()
    {
        // Arrange
        const string listId = "ListId";
        var value = new SomeObject
        {
            Id = "cacheKey",
            Name = "Some Name"
        };
        _databaseMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(new[] { new HashEntry(nameof(SomeObject), JsonConvert.SerializeObject(value)) })
            .Verifiable();

        // Act
        var result = await _repository.GetList(listId);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equivalent(value, result[0]);
        _databaseMock.Verify();
    }

    [Fact]
    public async Task GetList_Returns_Empty_List()
    {
        // Arrange
        _databaseMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<HashEntry>())
            .Verifiable();

        // Act
        var result = await _repository.GetList("ListId");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetList_Adds_Items_Using_CoreSetList()
    {
        // Arrange
        const string listId = "ListId";
        var value = new SomeObject
        {
            Id = "cacheKey",
            Name = "Some Name"
        };

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        memoryCache.Set($"{nameof(SomeObject)}:{listId}", new List<SomeObject> { value });

        var memoryCacheRepo = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        _databaseMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<HashEntry>());
        _repository.SetNext(memoryCacheRepo);

        // Act
        var result = await _repository
            .AdaptObjectToKey(obj => obj.Id)
            .GetList(listId, true, CancellationToken.None);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(value, result[0]);
        _databaseMock.Verify(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None), Times.Once);
    }


    [Fact]
    public async Task Throws_When_ObjectToKey_Is_Not_Defined()
    {
        // Arrange
        const string listId = "ListId";
        var value = new SomeObject
        {
            Id = "cacheKey",
            Name = "Some Name"
        };

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        memoryCache.Set($"{nameof(SomeObject)}:{listId}", new List<SomeObject> { value });

        var memoryCacheRepo = new MemoryCacheRepository<SomeObject, string>(
            Mock.Of<IDateTimeProvider>(),
            memoryCache,
            Options.Create(new MemoryCacheRepositoryOptions { TimeToLiveInSeconds = 60 }));

        _databaseMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(Array.Empty<HashEntry>());
        _repository.SetNext(memoryCacheRepo);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _repository.GetList(listId, true, CancellationToken.None));
    }
}